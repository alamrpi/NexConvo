using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Knowledge.Application.Common.Interfaces;
using NexConvo.Knowledge.Domain.Entities;

namespace NexConvo.Knowledge.Application.Features.KnowledgeBase.Commands;

public sealed class UploadKnowledgeDocumentCommandHandler(
    IKnowledgeDbContext db,
    ITenantContext tenant,
    IKnowledgeIngestionJobRunner jobRunner,
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

        var document = new KnowledgeDocument(tenantId, cmd.FileName, cmd.ContentHash);
        document.CreatedByUserId = cmd.ActorUserId;

        db.KnowledgeDocuments.Add(document);

        db.KnowledgeAuditLogs.Add(new KnowledgeAuditLog(
            "knowledge.upload",
            tenantId,
            cmd.ActorUserId,
            $"fileName={cmd.FileName};hash={cmd.ContentHash[..8]}",
            DateTimeOffset.UtcNow));

        await db.SaveChangesAsync(ct);

        jobRunner.EnqueueIngestion(document.Id, tenantId);

        logger.LogInformation("Knowledge document {DocumentId} created and queued for ingestion", document.Id);

        return Result.Success(document.Id);
    }
}
