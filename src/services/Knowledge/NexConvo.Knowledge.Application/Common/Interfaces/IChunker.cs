namespace NexConvo.Knowledge.Application.Common.Interfaces;

/// <summary>
/// One chunk produced by <see cref="IChunker"/>, ordered and token-counted but not yet
/// embedded. Maps 1:1 onto <see cref="KnowledgeChunkWrite"/> minus the embedding vector,
/// which the ingestion pipeline attaches after calling the embedding provider.
/// </summary>
/// <param name="Content">The chunk text, ready to be sent to the embedding model.</param>
/// <param name="Ordinal">Zero-based position of the chunk within the document.</param>
/// <param name="TokenCount">cl100k token count of <paramref name="Content"/>.</param>
public sealed record ChunkResult(string Content, int Ordinal, int TokenCount);

/// <summary>
/// Splits extracted document text into ordered, token-bounded chunks ready for embedding.
/// Pure text-in/text-out logic — no I/O, no persistence, no tenant context.
/// </summary>
public interface IChunker
{
    /// <summary>
    /// Splits <paramref name="text"/> into sentence-boundary-aware chunks, each within the
    /// chunker's token budget, with a small overlap carried between consecutive chunks so
    /// retrieval doesn't lose context at a chunk seam.
    /// </summary>
    /// <param name="text">The full extracted document text.</param>
    /// <param name="languageHint">
    /// Optional BCP-47-ish hint (e.g. "bn", "en") for sentence-boundary heuristics. Chunkers
    /// that don't need it may ignore it.
    /// </param>
    IReadOnlyList<ChunkResult> Chunk(string text, string? languageHint);
}
