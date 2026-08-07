namespace NexConvo.Knowledge.Application.Common.Interfaces;

/// <summary>A chunk of document text plus its embedding, ready to persist.</summary>
/// <param name="Content">The chunk text sent to the embedding model.</param>
/// <param name="Ordinal">Zero-based position of the chunk within the document.</param>
/// <param name="TokenCount">Token count of the content at chunking time.</param>
/// <param name="Embedding">The embedding vector — must match the schema dimension (1024).</param>
public sealed record KnowledgeChunkWrite(
    string Content,
    int Ordinal,
    int TokenCount,
    float[] Embedding);

/// <summary>
/// The single write path for embedded chunks. Implemented in Infrastructure, where the
/// embedding is mapped onto the EF shadow vector column; writes go through the RLS-scoped
/// connection so a chunk can never land in another tenant's partition.
/// </summary>
public interface IKnowledgeChunkWriter
{
    /// <summary>
    /// Replaces the chunks of <paramref name="documentId"/> at <paramref name="documentVersion"/>
    /// for the current tenant: deletes any existing rows for that (document, version) pair and
    /// inserts <paramref name="chunks"/>, all inside one transaction, so a re-run of ingestion for
    /// the same version is idempotent (Standard 18) — never leaves duplicate or partial chunk sets.
    /// Rejects embeddings whose length differs from the schema dimension; the transaction is rolled
    /// back and nothing is written when that happens.
    /// </summary>
    Task WriteChunksAsync(
        Guid documentId,
        int documentVersion,
        IReadOnlyList<KnowledgeChunkWrite> chunks,
        CancellationToken cancellationToken);
}
