using NexConvo.BuildingBlocks.Ai.Models;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace NexConvo.BuildingBlocks.Ai.Services;

public interface IAiProviderService
{
    /// <summary>
    /// Generates a response stream from the provider.
    /// </summary>
    /// <param name="prompt">The user prompt</param>
    /// <param name="systemPrompt">Optional system prompt/context</param>
    /// <param name="apiKey">The decrypted API key for the provider</param>
    /// <param name="model">The model to use</param>
    /// <param name="baseUrl">Optional custom Base URL (e.g. for OpenRouter)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An asynchronous stream of AI chunks</returns>
    IAsyncEnumerable<AiStreamChunk> GenerateStreamAsync(
        string prompt,
        string? systemPrompt,
        string apiKey,
        string model,
        string? baseUrl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a list of available models from the provider.
    /// </summary>
    /// <param name="apiKey">The decrypted API key for the provider</param>
    /// <param name="baseUrl">Optional custom Base URL</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of available models</returns>
    Task<IReadOnlyList<ModelDto>> GetAvailableModelsAsync(
        string apiKey,
        string? baseUrl = null,
        CancellationToken cancellationToken = default);
}
