namespace NexConvo.BuildingBlocks.Ai.Services;

/// <summary>
/// Whether a piece of text is being embedded as a search <see cref="Query"/> or as a
/// stored <see cref="Document"/>. This is load-bearing: retrieval embedders encode the two
/// asymmetrically (BGE-M3 prepends a query instruction; Cohere sets input_type). Encoding a
/// query as a document silently degrades retrieval quality, so callers MUST declare intent.
/// </summary>
public enum EmbeddingInputType
{
    Query,
    Document
}

/// <summary>
/// Turns text into a dense vector for semantic retrieval. Mirrors the chat-side
/// <see cref="IAiProviderService"/>: one swappable abstraction, one implementation per provider,
/// resolved through <see cref="IEmbeddingProviderFactory"/>.
/// </summary>
public interface IEmbeddingProviderService
{
    /// <summary>The fixed vector length this provider produces (1024 for BGE-M3 / Cohere).</summary>
    int Dimensions { get; }

    /// <summary>The model identifier this provider actually embeds with (e.g. "BAAI/bge-m3",
    /// "embed-multilingual-v3.0") — persisted onto the KnowledgeDocument so the UI reports which
    /// model really produced a document's chunks, not a stale constructor default.</summary>
    string ModelId { get; }

    /// <summary>
    /// Embeds a single piece of text.
    /// </summary>
    /// <param name="text">The text to embed</param>
    /// <param name="inputType">Whether the text is a search query or a stored document</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A vector of length <see cref="Dimensions"/></returns>
    Task<float[]> EmbedAsync(string text, EmbeddingInputType inputType, CancellationToken cancellationToken);

    /// <summary>
    /// Embeds a batch of texts in a single request. The returned vectors are in the same order
    /// as the input texts.
    /// </summary>
    /// <param name="texts">The texts to embed</param>
    /// <param name="inputType">Whether the texts are search queries or stored documents</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>One vector of length <see cref="Dimensions"/> per input text, in input order</returns>
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        EmbeddingInputType inputType,
        CancellationToken cancellationToken);
}
