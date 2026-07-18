using Microsoft.EntityFrameworkCore;
using NexConvo.Knowledge.Application.Common.Interfaces;
using Pgvector;

namespace NexConvo.Knowledge.Infrastructure.Persistence;

/// <summary>
/// Hybrid lexical + vector similarity search over <c>knowledge_chunks</c>
/// (CHATBOT-ARCHITECTURE.md §7.4/§12; improve-short-query-retrieval design.md). Uses raw SQL
/// because the embedding column is an EF shadow property with no LINQ-mapped distance operator,
/// and because RRF fusion is cheapest to compute in one round trip; the query still rides the
/// RLS-scoped connection the caller opened, so no tenant_id filter appears here — RLS is the
/// isolation boundary (skill Standard 6).
/// </summary>
public sealed class KnowledgeChunkRepository(KnowledgeDbContext db) : IKnowledgeChunkRepository
{
    /// <summary>
    /// Reciprocal Rank Fusion constant. Industry-standard default; see design.md's "MinScore is
    /// compared against the fused RRF score" decision — RRF combines rankings, not raw scores, so
    /// vector cosine similarity (0-1) and ts_rank (unbounded) never need comparable scales.
    /// </summary>
    private const int RrfK = 60;

    /// <summary>
    /// A generous over-fetch per signal before fusion — RRF needs each candidate's rank within its
    /// own list, so both the vector and lexical candidate sets must be wider than topK for fusion
    /// to have material outside the final topK to promote.
    /// </summary>
    private const int CandidatePoolMultiplier = 4;

    public async Task<IReadOnlyList<ChunkMatch>> SimilaritySearchAsync(
        string queryText, float[] queryEmbedding, int topK, double minScore, CancellationToken cancellationToken)
    {
        var query = new Vector(queryEmbedding);
        var candidatePoolSize = topK * CandidatePoolMultiplier;

        // Two ranked candidate sets fused via Reciprocal Rank Fusion (1/(k+rank) per list, summed):
        // vector_ranked = cosine similarity via the HNSW-indexed <=> operator (bound parameter,
        // never string-interpolated, so it rides the index); lexical_ranked = PostgreSQL full-text
        // search via ts_rank against the generated content_tsv column (simple config — see
        // design.md's "not a Bengali-specific text search configuration" decision), GIN-indexed.
        // FULL OUTER JOIN so a chunk found by only one signal still gets a (partial) fused score.
        var rows = await db.Database
            .SqlQuery<ChunkSearchRow>(
                $"""
                 WITH query_terms AS (
                     -- plainto_tsquery/websearch_to_tsquery AND every token together, so a natural
                     -- phrasing like "Tell me about the roadmap" would never match a chunk
                     -- containing only "roadmap". An OR-query over the tokenized terms lets ANY
                     -- shared word lift a chunk instead (design.md: lexical search's job is
                     -- exact-term recall, not full boolean-query semantics). Uses the simple_nostop
                     -- configuration (see migration AddContentTsvForHybridSearch) so filler words
                     -- like "the"/"of"/"about" are excluded from the OR-list entirely — otherwise an
                     -- unrelated chunk could match purely because both texts contain "the".
                     SELECT to_tsquery('simple_nostop', string_agg(lexeme, ' | ')) AS q
                     FROM unnest(tsvector_to_array(to_tsvector('simple_nostop', {queryText}))) AS lexeme
                 ),
                 vector_ranked AS (
                     SELECT id, knowledge_document_id, content,
                            1 - (embedding <=> {query}) AS vector_score,
                            row_number() OVER (ORDER BY embedding <=> {query}) AS rank
                     FROM knowledge_chunks
                     WHERE is_active = true
                     ORDER BY embedding <=> {query}
                     LIMIT {candidatePoolSize}
                 ),
                 lexical_ranked AS (
                     SELECT id, knowledge_document_id, content,
                            ts_rank(content_tsv, query_terms.q) AS lexical_score,
                            row_number() OVER (ORDER BY ts_rank(content_tsv, query_terms.q) DESC) AS rank
                     FROM knowledge_chunks, query_terms
                     WHERE is_active = true AND query_terms.q IS NOT NULL AND content_tsv @@ query_terms.q
                     ORDER BY ts_rank(content_tsv, query_terms.q) DESC
                     LIMIT {candidatePoolSize}
                 )
                 SELECT
                     COALESCE(v.id, l.id) AS "ChunkId",
                     COALESCE(v.knowledge_document_id, l.knowledge_document_id) AS "DocumentId",
                     COALESCE(v.content, l.content) AS "Content",
                     -- Raw RRF (sum of 1/(k+rank) per list) tops out at 2/(k+1), far below any
                     -- realistic MinScore (e.g. 0.55) on its own 0-1 scale. Normalize by that
                     -- theoretical max so a chunk ranked #1 on both signals scores 1.0 and
                     -- MinScore keeps the same "is this good enough to trust" meaning it always
                     -- had (design.md decision: MinScore compares against the fused score).
                     (COALESCE(1.0 / ({RrfK} + v.rank), 0.0) + COALESCE(1.0 / ({RrfK} + l.rank), 0.0))
                         / (2.0 / ({RrfK} + 1)) AS "Score"
                 FROM vector_ranked v
                 FULL OUTER JOIN lexical_ranked l ON v.id = l.id
                 ORDER BY "Score" DESC
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
