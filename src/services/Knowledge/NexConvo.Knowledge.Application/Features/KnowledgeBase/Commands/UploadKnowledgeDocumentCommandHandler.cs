using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Knowledge.Application.Common.Interfaces;
using NexConvo.Knowledge.Application.Common.Security;
using NexConvo.Knowledge.Domain.Entities;
using NexConvo.Knowledge.Domain.Enums;

namespace NexConvo.Knowledge.Application.Features.KnowledgeBase.Commands;

/// <summary>
/// Branches per <see cref="SourceType"/> (File/Url/Text/Faq) but converges every source on one
/// create/dedup/audit/enqueue path (Slice 3 step 7). All four store their source bytes to the
/// workspace S3 bucket so the ingestion job always downloads from one uniform place — File and Url
/// bytes are the client/remote-supplied content; Text and Faq synthesize a text/plain blob so the
/// job's "download from S3" step never needs a source-type-specific branch of its own.
/// </summary>
public sealed class UploadKnowledgeDocumentCommandHandler(
    IKnowledgeDbContext db,
    ITenantContext tenant,
    IKnowledgeIngestionJobRunner jobRunner,
    IS3StorageService s3Storage,
    IDnsResolver dnsResolver,
    IUrlContentFetcher urlFetcher,
    ILogger<UploadKnowledgeDocumentCommandHandler> logger)
    : IRequestHandler<UploadKnowledgeDocumentCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(UploadKnowledgeDocumentCommand cmd, CancellationToken ct)
    {
        var tenantId = tenant.TenantId;

        // Idempotency: same content already exists — return the existing document.
        var existing = await db.KnowledgeDocuments
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId
                  && x.ContentHash == cmd.ContentHash
                  && x.IsActive,
                ct);

        if (existing is not null)
        {
            logger.LogInformation(
                "Duplicate content hash for document {DocumentId} — returning existing id",
                existing.Id);
            return Result.Success(existing.Id);
        }

        var document = cmd.SourceType switch
        {
            SourceType.File => await CreateFileDocumentAsync(cmd, tenantId, ct),
            SourceType.Url => await CreateUrlDocumentAsync(cmd, tenantId, ct),
            SourceType.Text => await CreateTextDocumentAsync(cmd, tenantId, ct),
            SourceType.Faq => await CreateFaqDocumentAsync(cmd, tenantId, ct),
            _ => throw new NotSupportedException($"Source type '{cmd.SourceType}' is not supported."),
        };

        document.CreatedByUserId = cmd.ActorUserId;

        db.KnowledgeDocuments.Add(document);

        db.KnowledgeAuditLogs.Add(new KnowledgeAuditLog(
            "knowledge.upload",
            tenantId,
            cmd.ActorUserId,
            $"sourceType={cmd.SourceType};hash={cmd.ContentHash[..Math.Min(8, cmd.ContentHash.Length)]}",
            DateTimeOffset.UtcNow));

        await db.SaveChangesAsync(ct);

        jobRunner.EnqueueIngestion(document.Id, tenantId);

        logger.LogInformation(
            "Knowledge document {DocumentId} ({SourceType}) created and queued for ingestion",
            document.Id, cmd.SourceType);

        return Result.Success(document.Id);
    }

    /// <summary>
    /// File source: the client-supplied stream is pushed to S3 as-is. <see cref="IS3StorageService"/>
    /// itself guards on the tenant's cached S3 health and throws <c>ConnectionUnhealthyException</c>
    /// (→ 409 via the shared exception-handling middleware) BEFORE any bytes are written — nothing is
    /// persisted or enqueued when that happens, since this call happens before db.Add/SaveChanges.
    /// </summary>
    private async Task<KnowledgeDocument> CreateFileDocumentAsync(
        UploadKnowledgeDocumentCommand cmd, Guid tenantId, CancellationToken ct)
    {
        var objectKey = BuildObjectKey(tenantId, cmd.ContentHash, cmd.FileName ?? cmd.Title);

        await s3Storage.PutObjectAsync(
            tenantId, objectKey, cmd.FileStream!, ResolveContentType(cmd.FileName), ct);

        var document = new KnowledgeDocument(tenantId, cmd.FileName ?? cmd.Title, cmd.ContentHash);
        document.SetS3ObjectKey(objectKey);
        return document;
    }

    /// <summary>
    /// Url source: resolves the host via DNS and rejects any address in a private/loopback/
    /// link-local range BEFORE issuing the HTTP fetch (SSRF guard — string-matching the URL alone
    /// is insufficient against DNS rebinding). Only after the guard passes does
    /// <see cref="IUrlContentFetcher"/> perform the resilient, https-only, size-capped fetch; the
    /// fetched bytes are then stored to S3 for pipeline uniformity and audit trail.
    /// </summary>
    private async Task<KnowledgeDocument> CreateUrlDocumentAsync(
        UploadKnowledgeDocumentCommand cmd, Guid tenantId, CancellationToken ct)
    {
        var uri = new Uri(cmd.SourceUrl!, UriKind.Absolute);
        await EnsureNotPrivateNetworkAsync(uri, ct);

        var fetched = await urlFetcher.FetchAsync(uri, ct);

        var objectKey = BuildObjectKey(tenantId, cmd.ContentHash, "source.bin");
        using var content = new MemoryStream(fetched.Content, writable: false);
        await s3Storage.PutObjectAsync(
            tenantId, objectKey, content, fetched.ContentType ?? "application/octet-stream", ct);

        var document = KnowledgeDocument.ForUrl(tenantId, cmd.Title, cmd.SourceUrl!, cmd.ContentHash);
        document.SetS3ObjectKey(objectKey);
        return document;
    }

    /// <summary>Text source: the raw text is stored to S3 as text/plain so the job's download step stays uniform.</summary>
    private async Task<KnowledgeDocument> CreateTextDocumentAsync(
        UploadKnowledgeDocumentCommand cmd, Guid tenantId, CancellationToken ct)
    {
        var objectKey = BuildObjectKey(tenantId, cmd.ContentHash, "source.txt");
        using var content = new MemoryStream(Encoding.UTF8.GetBytes(cmd.RawText!));
        await s3Storage.PutObjectAsync(tenantId, objectKey, content, "text/plain", ct);

        var document = KnowledgeDocument.ForText(tenantId, cmd.Title, cmd.ContentHash);
        document.SetS3ObjectKey(objectKey);
        return document;
    }

    /// <summary>
    /// Faq source: pairs are serialized "Q: ...\nA: ..." (blank-line delimited) and stored to S3 as
    /// text/plain — the ingestion job recognizes SourceType==Faq and chunks 1-pair-per-chunk instead
    /// of running the sentence-based chunker, since the pairs themselves are the desired boundary.
    /// </summary>
    private async Task<KnowledgeDocument> CreateFaqDocumentAsync(
        UploadKnowledgeDocumentCommand cmd, Guid tenantId, CancellationToken ct)
    {
        var serialized = string.Join(
            "\n\n", cmd.FaqPairs!.Select(p => $"Q: {p.Question}\nA: {p.Answer}"));

        var objectKey = BuildObjectKey(tenantId, cmd.ContentHash, "source.txt");
        using var content = new MemoryStream(Encoding.UTF8.GetBytes(serialized));
        await s3Storage.PutObjectAsync(tenantId, objectKey, content, "text/plain", ct);

        var document = KnowledgeDocument.ForFaq(tenantId, cmd.Title, cmd.ContentHash);
        document.SetS3ObjectKey(objectKey);
        return document;
    }

    private async Task EnsureNotPrivateNetworkAsync(Uri uri, CancellationToken ct)
    {
        var addresses = await dnsResolver.ResolveAsync(uri.Host, ct);

        if (addresses.Count == 0)
            throw new NotSupportedException($"Could not resolve host '{uri.Host}'.");

        if (addresses.Any(PrivateNetworkGuard.IsPrivateOrLinkLocal))
        {
            throw new NotSupportedException(
                $"URL host '{uri.Host}' resolves to a private or link-local address and cannot be fetched.");
        }
    }

    private static string BuildObjectKey(Guid tenantId, string contentHash, string fileName)
        => $"knowledge/{tenantId}/{contentHash}/{fileName}";

    private static string ResolveContentType(string? fileName)
    {
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".md" => "text/markdown",
            ".csv" => "text/csv",
            ".txt" => "text/plain",
            _ => "application/octet-stream",
        };
    }
}
