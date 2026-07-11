using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.Knowledge.Application.Common.Interfaces;
using NexConvo.Knowledge.Domain.Entities;
using NexConvo.Knowledge.Infrastructure.Persistence.Configurations;
using Pgvector;

namespace NexConvo.Knowledge.Infrastructure.Persistence;

/// <summary>
/// The single write path for embedded chunks (CHATBOT-ARCHITECTURE.md §12/§13).
/// Depends on the concrete <see cref="KnowledgeDbContext"/> because the embedding is an EF
/// shadow property — only reachable via ChangeTracker entries, not the application interface.
/// The context's RLS-scoped connection guarantees the chunk lands in the caller's tenant.
/// </summary>
public sealed class KnowledgeChunkWriter(
    KnowledgeDbContext db,
    ITenantContext tenant,
    ILogger<KnowledgeChunkWriter> logger) : IKnowledgeChunkWriter
{
    public async Task WriteChunksAsync(
        Guid documentId,
        int documentVersion,
        IReadOnlyList<KnowledgeChunkWrite> chunks,
        CancellationToken cancellationToken)
    {
        foreach (var chunk in chunks)
        {
            if (chunk.Embedding.Length != KnowledgeChunkConfiguration.EmbeddingDimensions)
            {
                throw new ArgumentException(
                    $"Embedding for chunk ordinal {chunk.Ordinal} has {chunk.Embedding.Length} dimensions; " +
                    $"the knowledge_chunks.embedding column requires exactly " +
                    $"{KnowledgeChunkConfiguration.EmbeddingDimensions}.",
                    nameof(chunks));
            }
        }

        var tenantId = tenant.TenantId;

        // Delete-then-insert in one transaction so a re-run of ingestion for the same
        // (document, version) — e.g. a retried Hangfire job — is idempotent (Standard 18):
        // never leaves duplicate rows or a partial chunk set if the process dies mid-write.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var existing = await db.KnowledgeChunks
            .Where(c => c.DocumentId == documentId && c.DocumentVersion == documentVersion)
            .ToListAsync(cancellationToken);
        db.KnowledgeChunks.RemoveRange(existing);

        foreach (var chunk in chunks)
        {
            var entity = new KnowledgeChunk(
                tenantId,
                documentId,
                chunk.Content,
                chunk.Ordinal,
                chunk.TokenCount,
                documentVersion);

            db.KnowledgeChunks.Add(entity);
            db.Entry(entity).Property("Embedding").CurrentValue = new Vector(chunk.Embedding);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Persisted {ChunkCount} chunks for document {DocumentId} at version {DocumentVersion}, replacing {ExistingCount} prior chunks",
            chunks.Count, documentId, documentVersion, existing.Count);
    }
}
