using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NexConvo.BuildingBlocks.Ai.Services;

/// <summary>
/// Alternative embedding provider: Cohere's managed <c>embed-multilingual-v3.0</c> via the v2 embed
/// API. Asymmetric encoding is expressed through Cohere's <c>input_type</c>
/// (search_query vs search_document). The API key comes from configuration and is never logged.
/// </summary>
public class CohereEmbeddingProviderService : IEmbeddingProviderService
{
    private const string ModelId = "embed-multilingual-v3.0";
    private const string EmbedEndpoint = "https://api.cohere.com/v2/embed";
    private const int EmbeddingDimensions = 1024;

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CohereEmbeddingProviderService> _logger;

    public CohereEmbeddingProviderService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<CohereEmbeddingProviderService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public int Dimensions => EmbeddingDimensions;

    public async Task<float[]> EmbedAsync(string text, EmbeddingInputType inputType, CancellationToken cancellationToken)
    {
        var vectors = await EmbedBatchAsync(new[] { text }, inputType, cancellationToken);
        return vectors[0];
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts, EmbeddingInputType inputType, CancellationToken cancellationToken)
    {
        var apiKey = _configuration["EMBEDDING:COHERE:APIKEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Cohere API key is not configured (EMBEDDING__COHERE__APIKEY).");
        }

        var requestBody = new
        {
            model = ModelId,
            texts,
            input_type = ToCohereInputType(inputType),
            embedding_types = new[] { "float" }
        };

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, EmbedEndpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var floats = document.RootElement.GetProperty("embeddings").GetProperty("float");
        var vectors = new List<float[]>(floats.GetArrayLength());
        foreach (var embedding in floats.EnumerateArray())
        {
            vectors.Add(ParseVector(embedding));
        }

        LogTokenUsage(texts.Count, document.RootElement);
        return vectors;
    }

    private static string ToCohereInputType(EmbeddingInputType inputType) => inputType switch
    {
        EmbeddingInputType.Query => "search_query",
        EmbeddingInputType.Document => "search_document",
        _ => throw new NotSupportedException($"Embedding input type '{inputType}' is not supported.")
    };

    private static float[] ParseVector(JsonElement embedding)
    {
        var vector = new float[embedding.GetArrayLength()];
        var i = 0;
        foreach (var component in embedding.EnumerateArray())
        {
            vector[i++] = component.GetSingle();
        }

        if (vector.Length != EmbeddingDimensions)
        {
            throw new InvalidOperationException(
                $"Cohere returned a {vector.Length}-dimension vector; expected {EmbeddingDimensions}.");
        }

        return vector;
    }

    private void LogTokenUsage(int count, JsonElement root)
    {
        int? tokens = root.TryGetProperty("meta", out var meta)
            && meta.TryGetProperty("billed_units", out var billed)
            && billed.TryGetProperty("input_tokens", out var input)
                ? input.GetInt32()
                : null;

        // Never log the API key or the embedded text (Standards 13 & 9); context attaches via LogContext.
        _logger.LogInformation("Embedded {Count} texts with {Provider} ({TokenCount} tokens)", count, "Cohere", tokens);
    }
}
