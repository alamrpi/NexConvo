using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Domain.Entities;

namespace NexConvo.Chat.Application.Features.KnowledgeBase.Commands;

public sealed class DeleteKnowledgeDocumentCommandHandler(
    IChatDbContext db,
    ITenantContext tenant,
    ILogger<DeleteKnowledgeDocumentCommandHandler> logger)
    : IRequestHandler<DeleteKnowledgeDocumentCommand, Result>
{
    public async Task<Result> Handle(DeleteKnowledgeDocumentCommand cmd, CancellationToken ct)
    {
        var tenantId = tenant.TenantId;

        var document = await db.KnowledgeDocuments
            .FirstOrDefaultAsync(
                x => x.Id == cmd.DocumentId && x.TenantId == tenantId && x.IsActive,
                ct);

        if (document is null)
            return Result.NotFound($"Knowledge document {cmd.DocumentId} not found.");

        document.SetActive(false);

        // Soft-delete all embedding chunks belonging to this document.
        var chunks = await db.KnowledgeChunks
            .Where(c => c.DocumentId == cmd.DocumentId && c.TenantId == tenantId && c.IsActive)
            .ToListAsync(ct);

        foreach (var chunk in chunks)
            chunk.SetActive(false);

        db.ChatAuditLogs.Add(new ChatAuditLog(
            "chat.knowledge.delete",
            tenantId,
            cmd.ActorUserId,
            $"documentId={cmd.DocumentId};chunksDeleted={chunks.Count}",
            DateTimeOffset.UtcNow));

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Knowledge document {DocumentId} soft-deleted ({ChunkCount} chunks)",
            cmd.DocumentId, chunks.Count);

        return Result.Success();
    }
}
