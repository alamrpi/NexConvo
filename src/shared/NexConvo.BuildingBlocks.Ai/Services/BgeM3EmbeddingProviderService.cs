using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace NexConvo.BuildingBlocks.Ai.Services;

/// <summary>
/// Default embedding provider: the self-hosted "Infinity" server running BGE-M3, exposed over an
/// OpenAI-compatible <c>POST {baseUrl}/embeddings</c> endpoint. Query inputs get BGE-M3's retrieval
/// instruction prepended (asymmetric encoding); document inputs are sent raw.
/// </summary>
public class BgeM3EmbeddingProviderService : IEmbeddingProviderService
{
    private const string ModelIdValue = "BAAI/bge-m3";
    private const string DefaultBaseUrl = "http://bge-m3:7997";
    private const string QueryInstruction = "Represent this sentence for searching relevant passages: ";
    private const int EmbeddingDimensions = 1024;

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BgeM3EmbeddingProviderService> _logger;

    public BgeM3EmbeddingProviderService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<BgeM3EmbeddingProviderService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public int Dimensions => EmbeddingDimensions;

    public string ModelId => ModelIdValue;

    public async Task<float[]> EmbedAsync(string text, EmbeddingInputType inputType, CancellationToken cancellationToken)
    {
        var vectors = await EmbedBatchAsync(new[] { text }, inputType, cancellationToken);
        return vectors[0];
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts, EmbeddingInputType inputType, CancellationToken cancellationToken)
    {
        var baseUrl = _configuration["EMBEDDING:BGEM3:BASEURL"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = DefaultBaseUrl;
        }

        var input = inputType == EmbeddingInputType.Query
            ? texts.Select(t => QueryInstruction + t).ToArray()
            : texts.ToArray();

        var requestBody = new { model = ModelId, input };

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/embeddings")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var data = document.RootElement.GetProperty("data");
        var vectors = new List<float[]>(data.GetArrayLength());
        foreach (var item in data.EnumerateArray())
        {
            vectors.Add(ParseVector(item.GetProperty("embedding")));
        }

        LogTokenUsage(texts.Count, document.RootElement);
        return vectors;
    }

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
                $"BGE-M3 returned a {vector.Length}-dimension vector; expected {EmbeddingDimensions}.");
        }

        return vector;
    }

    private void LogTokenUsage(int count, JsonElement root)
    {
        int? tokens = root.TryGetProperty("usage", out var usage)
            && usage.TryGetProperty("total_tokens", out var total)
                ? total.GetInt32()
                : null;

        // Never log the embedded text (PII, Standard 9); TenantId/CorrelationId attach via LogContext.
        _logger.LogInformation(
            "Embedded {Count} texts with {Provider} model {ModelId} ({TokenCount} tokens)",
            count, "BgeM3", ModelId, tokens);
    }
}
