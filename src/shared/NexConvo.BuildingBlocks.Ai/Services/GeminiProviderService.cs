using NexConvo.BuildingBlocks.Ai.Models;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace NexConvo.BuildingBlocks.Ai.Services;

public class GeminiProviderService(HttpClient httpClient) : IAiProviderService
{
    private const string DefaultBaseUrl = "https://generativelanguage.googleapis.com/v1beta";

    public async IAsyncEnumerable<AiStreamChunk> GenerateStreamAsync(
        string prompt,
        string? systemPrompt,
        string apiKey,
        string model,
        string? baseUrl = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var baseEndpoint = baseUrl ?? DefaultBaseUrl;
        var requestUrl = $"{baseEndpoint}/models/{model}:streamGenerateContent?alt=sse";

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        requestMessage.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);

        object requestBody;
        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            requestBody = new
            {
                contents = new[] { new { role = "user", parts = new[] { new { text = prompt } } } },
                systemInstruction = new { parts = new[] { new { text = systemPrompt } } }
            };
        }
        else
        {
            requestBody = new
            {
                contents = new[] { new { role = "user", parts = new[] { new { text = prompt } } } }
            };
        }

        requestMessage.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new System.IO.StreamReader(stream);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (line.StartsWith("data: ", System.StringComparison.Ordinal))
            {
                var json = line.Substring(6);
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var candidate = candidates[0];

                    string? chunkContent = null;
                    if (candidate.TryGetProperty("content", out var content)
                        && content.TryGetProperty("parts", out var parts)
                        && parts.GetArrayLength() > 0
                        && parts[0].TryGetProperty("text", out var textElement))
                    {
                        chunkContent = textElement.GetString();
                    }

                    string? finishReason = null;
                    if (candidate.TryGetProperty("finishReason", out var finishReasonElement)
                        && finishReasonElement.ValueKind != JsonValueKind.Null)
                    {
                        finishReason = finishReasonElement.GetString();
                    }

                    if (chunkContent != null || finishReason != null)
                    {
                        yield return new AiStreamChunk(chunkContent ?? string.Empty, finishReason, null, null);
                    }
                }
            }
        }
    }
}
