using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexConvo.Knowledge.Api.Extensions;
using NexConvo.Knowledge.Application.Features.KnowledgeBase.Commands;
using NexConvo.Knowledge.Application.Features.KnowledgeBase.Queries.GetKnowledgeDocumentById;
using NexConvo.Knowledge.Application.Features.KnowledgeBase.Queries.GetKnowledgeDocumentsPaged;
using NexConvo.Knowledge.Domain.Enums;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NexConvo.Knowledge.Api.Controllers;

/// <summary>
/// Knowledge Base management endpoints.
/// All actions require the `knowledge:manage` permission (Standard 12 — deny-by-default RBAC).
/// </summary>
[ApiController]
[Route("api/v1/knowledge-documents")]
[Authorize(Policy = "knowledge:manage")]
public sealed class KnowledgeBaseController(ISender sender) : ControllerBase
{
    /// <summary>Returns a paginated list of knowledge documents for the current tenant.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DocumentStatus? status = null,
        [FromQuery] SourceType? sourceType = null,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new GetKnowledgeDocumentsPagedQuery(page, pageSize, status, sourceType), cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>Returns a single knowledge document with chunk preview.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromQuery] int chunkPage = 1,
        [FromQuery] int chunkPageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new GetKnowledgeDocumentByIdQuery(id, chunkPage, chunkPageSize), cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Uploads a knowledge document for async ingestion. Accepts multipart/form-data carrying
    /// exactly one of four source shapes (File/Url/Text/Faq) selected by <see cref="UploadDocumentFormRequest.SourceType"/>.
    /// The server computes the SHA-256 content hash itself (never trusts a client-supplied hash)
    /// and dispatches a single <see cref="UploadKnowledgeDocumentCommand"/> — all source-specific
    /// validation and persistence lives in the Application layer; this controller only maps the
    /// HTTP request and dispatches (Standard 1).
    /// Returns 202 Accepted with the document ID immediately; ingestion runs in the background.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(
        [FromForm] UploadDocumentFormRequest formRequest,
        CancellationToken cancellationToken = default)
    {
        var actorUserId = GetActorUserId();

        Stream? fileStream = null;
        string? fileName = null;
        string contentHash;
        IReadOnlyList<FaqPair>? faqPairs = null;

        switch (formRequest.SourceType)
        {
            case SourceType.File:
                if (formRequest.File is null)
                    return UnprocessableEntity("A file is required for a File upload.");

                var ms = new MemoryStream();
                await formRequest.File.CopyToAsync(ms, cancellationToken);
                ms.Position = 0;
                contentHash = Convert.ToHexString(SHA256.HashData(ms.ToArray())).ToLowerInvariant();
                fileStream = ms;
                fileName = formRequest.File.FileName;
                break;

            case SourceType.Url:
                contentHash = ComputeHash(formRequest.SourceUrl ?? string.Empty);
                break;

            case SourceType.Text:
                contentHash = ComputeHash(formRequest.RawText ?? string.Empty);
                break;

            case SourceType.Faq:
                faqPairs = ParseFaqPairs(formRequest.FaqPairsJson);
                contentHash = ComputeHash(string.Join(
                    "\n\n", faqPairs.Select(p => $"Q: {p.Question}\nA: {p.Answer}")));
                break;

            default:
                return UnprocessableEntity($"Unsupported source type '{formRequest.SourceType}'.");
        }

        try
        {
            var title = string.IsNullOrWhiteSpace(formRequest.Title)
                ? fileName ?? formRequest.SourceType.ToString()
                : formRequest.Title;

            var command = new UploadKnowledgeDocumentCommand(
                SourceType: formRequest.SourceType,
                Title: title,
                ContentHash: contentHash,
                ActorUserId: actorUserId,
                FileName: fileName,
                FileStream: fileStream,
                SourceUrl: formRequest.SourceUrl,
                RawText: formRequest.RawText,
                FaqPairs: faqPairs);

            var result = await sender.Send(command, cancellationToken);
            return result.ToAcceptedResult(id => Url.Action(nameof(GetById), new { id })!);
        }
        finally
        {
            if (fileStream is not null)
                await fileStream.DisposeAsync();
        }
    }

    /// <summary>Soft-deletes a knowledge document and all its chunks.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var actorUserId = GetActorUserId();
        var result = await sender.Send(new DeleteKnowledgeDocumentCommand(id, actorUserId), cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>Triggers re-embedding of an existing knowledge document.</summary>
    [HttpPost("{id:guid}/re-embed")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ReEmbed(Guid id, CancellationToken cancellationToken = default)
    {
        var actorUserId = GetActorUserId();
        var result = await sender.Send(new ReEmbedKnowledgeDocumentCommand(id, actorUserId), cancellationToken);
        return result.ToActionResult();
    }

    private Guid GetActorUserId()
    {
        var claim = User.FindFirst("sub")
            ?? User.FindFirst("user_id")
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim?.Value, out var id) ? id : Guid.Empty;
    }

    private static string ComputeHash(string content)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    /// <summary>
    /// FAQ pairs travel over multipart/form-data as a single JSON-encoded form field
    /// (an array of <c>{ "question": "...", "answer": "..." }</c> objects) since HTML form
    /// encoding has no native nested-list shape. Malformed/missing JSON yields an empty list —
    /// the command validator (Application layer) rejects it, keeping validation out of the controller.
    /// </summary>
    private static IReadOnlyList<FaqPair> ParseFaqPairs(string? faqPairsJson)
    {
        if (string.IsNullOrWhiteSpace(faqPairsJson))
            return Array.Empty<FaqPair>();

        try
        {
            var dto = JsonSerializer.Deserialize<List<FaqPairDto>>(
                faqPairsJson, JsonOptions) ?? [];
            return dto.Select(p => new FaqPair(p.Question ?? string.Empty, p.Answer ?? string.Empty)).ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<FaqPair>();
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

/// <summary>
/// Multipart/form-data binding for <see cref="KnowledgeBaseController.Upload"/>. Carries the
/// union of fields needed across all four <see cref="SourceType"/> shapes — only the fields
/// relevant to the selected <see cref="SourceType"/> are required (enforced by
/// <c>UploadKnowledgeDocumentCommandValidator</c> in the Application layer, not here).
/// </summary>
public sealed class UploadDocumentFormRequest
{
    public SourceType SourceType { get; set; } = SourceType.File;
    public string? Title { get; set; }
    public IFormFile? File { get; set; }
    public string? SourceUrl { get; set; }
    public string? RawText { get; set; }

    /// <summary>JSON-encoded array of <c>{ "question": "...", "answer": "..." }</c> for Faq uploads.</summary>
    public string? FaqPairsJson { get; set; }
}

/// <summary>Wire shape for one FAQ pair inside <see cref="UploadDocumentFormRequest.FaqPairsJson"/>.</summary>
internal sealed class FaqPairDto
{
    public string? Question { get; set; }
    public string? Answer { get; set; }
}
