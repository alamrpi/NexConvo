using NexConvo.Contracts.Enums;

namespace NexConvo.BuildingBlocks.Ai.Services;

/// <summary>
/// Resolves an <see cref="IEmbeddingProviderService"/>. Unlike the chat factory (which selects
/// per call), embeddings have a single active provider chosen from configuration
/// (<c>EMBEDDING:PROVIDER</c>) so ingestion and query embedding always agree.
/// </summary>
public interface IEmbeddingProviderFactory
{
    /// <summary>Returns the provider configured via <c>EMBEDDING:PROVIDER</c> (default BgeM3).</summary>
    IEmbeddingProviderService GetActiveProvider();

    IEmbeddingProviderService GetProvider(EmbeddingProviderType providerType);

    IEmbeddingProviderService GetProvider(string providerName);
}
