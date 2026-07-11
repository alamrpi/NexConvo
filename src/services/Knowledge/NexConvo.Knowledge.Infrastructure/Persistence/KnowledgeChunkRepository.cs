using Microsoft.EntityFrameworkCore;
using NexConvo.Knowledge.Application.Common.Interfaces;
using Pgvector;

namespace NexConvo.Knowledge.Infrastructure.Persistence;

/// <summary>
/// Cosine similarity search over <c>knowledge_chunks</c> (CHATBOT-ARCHITECTURE.md §7.4/§12).
/// Uses raw SQL because the embedding column is an EF shadow property with no LINQ-mapped
/// distance operator; the query still rides the RLS-scoped connection the caller opened, so
/// no tenant_id filter appears here — RLS is the isolation boundary (skill Standard 6).
/// </summary>
public sealed class KnowledgeChunkRepository(KnowledgeDbContext db) : IKnowledgeChunkRepository
{
    public async Task<IReadOnlyList<ChunkMatch>> SimilaritySearchAsync(
        float[] queryEmbedding, int topK, double minScore, CancellationToken cancellationToken)
    {
        var query = new Vector(queryEmbedding);

        // Cosine distance (<=>) ascending = most similar first; rides the HNSW index because the
        // vector is a bound parameter, never string-interpolated. Similarity = 1 - distance (§12).
        var rows = await db.Database
            .SqlQuery<ChunkSearchRow>(
                $"""
                 SELECT id AS "ChunkId", knowledge_document_id AS "DocumentId", content AS "Content",
                        1 - (embedding <=> {query}) AS "Score"
                 FROM knowledge_chunks
                 WHERE is_active = true
                 ORDER BY embedding <=> {query}
                 LIMIT {topK}
                 """)
            .ToListAsync(cancellationToken);

        return rows
            .Where(r => r.Score >= minScore)
            .Select(r => new ChunkMatch(r.ChunkId, r.DocumentId, r.Content, r.Score))
            .ToList();
    }

    private sealed record ChunkSearchRow(Guid ChunkId, Guid DocumentId, string Content, double Score);
}
