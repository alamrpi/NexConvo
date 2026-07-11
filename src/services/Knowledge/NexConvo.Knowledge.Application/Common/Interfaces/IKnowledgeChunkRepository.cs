namespace NexConvo.Knowledge.Application.Common.Interfaces;

/// <summary>
/// A ranked similarity match returned from <see cref="IKnowledgeChunkRepository.SimilaritySearchAsync"/>.
/// </summary>
public sealed record ChunkMatch(Guid ChunkId, Guid DocumentId, string Content, double Score);

/// <summary>
/// Cosine similarity search over <c>knowledge_chunks</c> (CHATBOT-ARCHITECTURE.md §7.4/§12).
/// Tenant scoping comes from PostgreSQL RLS on the underlying connection — implementations must
/// NOT add an application-level tenant filter (skill Standard 6: RLS is the isolation boundary).
/// </summary>
public interface IKnowledgeChunkRepository
{
    /// <summary>
    /// Returns the top <paramref name="topK"/> active chunks nearest to <paramref name="queryEmbedding"/>
    /// by cosine similarity, excluding any below <paramref name="minScore"/>, ordered best-first.
    /// </summary>
    Task<IReadOnlyList<ChunkMatch>> SimilaritySearchAsync(
        float[] queryEmbedding, int topK, double minScore, CancellationToken cancellationToken);
}
