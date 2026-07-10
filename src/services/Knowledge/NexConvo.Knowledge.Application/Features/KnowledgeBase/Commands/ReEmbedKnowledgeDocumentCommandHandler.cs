using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Knowledge.Application.Common.Interfaces;
using NexConvo.Knowledge.Domain.Entities;

namespace NexConvo.Knowledge.Application.Features.KnowledgeBase.Commands;

public sealed class ReEmbedKnowledgeDocumentCommandHandler(
    IKnowledgeDbContext db,
    ITenantContext tenant,
    IKnowledgeIngestionJobRunner jobRunner,
    ILogger<ReEmbedKnowledgeDocumentCommandHandler> logger)
    : IRequestHandler<ReEmbedKnowledgeDocumentCommand, Result>
{
    public async Task<Result> Handle(ReEmbedKnowledgeDocumentCommand cmd, CancellationToken ct)
    {
        var tenantId = tenant.TenantId;

        var document = await db.KnowledgeDocuments
            .FirstOrDefaultAsync(
                x => x.Id == cmd.DocumentId && x.TenantId == tenantId && x.IsActive,
                ct);

        if (document is null)
            return Result.NotFound($"Knowledge document {cmd.DocumentId} not found.");

        document.IncrementVersionAndResetToPending();

        db.KnowledgeAuditLogs.Add(new KnowledgeAuditLog(
            "knowledge.re-embed",
            tenantId,
            cmd.ActorUserId,
            $"documentId={cmd.DocumentId};newVersion={document.Version}",
            DateTimeOffset.UtcNow));

        await db.SaveChangesAsync(ct);

        jobRunner.EnqueueIngestion(document.Id, tenantId);

        logger.LogInformation(
            "Knowledge document {DocumentId} queued for re-embedding at version {Version}",
            document.Id, document.Version);

        return Result.Success();
    }
}
