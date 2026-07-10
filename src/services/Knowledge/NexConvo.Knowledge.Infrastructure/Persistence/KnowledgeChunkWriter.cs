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

        logger.LogInformation(
            "Persisted {ChunkCount} chunks for document {DocumentId} at version {DocumentVersion}",
            chunks.Count, documentId, documentVersion);
    }
}
