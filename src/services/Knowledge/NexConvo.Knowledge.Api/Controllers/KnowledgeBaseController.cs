using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexConvo.Knowledge.Api.Extensions;
using NexConvo.Knowledge.Application.Features.KnowledgeBase.Commands;
using System.Security.Cryptography;

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
    public IActionResult List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        // Slice 3: full query handler with filtering and pagination will be implemented here.
        return Ok(new { page, pageSize, items = Array.Empty<object>(), total = 0 });
    }

    /// <summary>Returns a single knowledge document with chunk preview.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult GetById(Guid id)
    {
        // Slice 3: full query handler will be implemented here.
        return NotFound();
    }

    /// <summary>
    /// Uploads a knowledge document for async ingestion.
    /// Accepts multipart/form-data file uploads; computes SHA-256 hash for deduplication.
    /// Returns 202 Accepted with the document ID immediately; ingestion runs in the background.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(
        [FromForm] UploadDocumentFormRequest formRequest,
        CancellationToken cancellationToken = default)
    {
        if (formRequest.File is null)
            return UnprocessableEntity("A file is required.");

        var actorUserId = GetActorUserId();

        using var ms = new MemoryStream();
        await formRequest.File.CopyToAsync(ms, cancellationToken);
        var fileBytes = ms.ToArray();
        var contentHash = Convert.ToHexString(SHA256.HashData(fileBytes)).ToLowerInvariant();

        var command = new UploadKnowledgeDocumentCommand(
            FileName: formRequest.File.FileName,
            ContentHash: contentHash,
            ActorUserId: actorUserId);

        var result = await sender.Send(command, cancellationToken);
        return result.ToAcceptedResult(id => Url.Action(nameof(GetById), new { id })!);
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
}

public sealed class UploadDocumentFormRequest
{
    public IFormFile? File { get; set; }
}
