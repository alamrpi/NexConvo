using NexConvo.BuildingBlocks.Ai.Models;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace NexConvo.BuildingBlocks.Ai.Services;

public abstract class OpenAiCompatibleProviderService : IAiProviderService
{
    private readonly HttpClient _httpClient;

    protected OpenAiCompatibleProviderService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    protected abstract string DefaultEndpoint { get; }

    protected virtual void AddProviderHeaders(HttpRequestMessage request) { }

    public async IAsyncEnumerable<AiStreamChunk> GenerateStreamAsync(
        string prompt,
        string? systemPrompt,
        string apiKey,
        string model,
        string? baseUrl = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var requestUrl = baseUrl ?? DefaultEndpoint;

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        AddProviderHeaders(requestMessage);

        var messages = new System.Collections.Generic.List<object>();
        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(new { role = "system", content = systemPrompt });
        }
        messages.Add(new { role = "user", content = prompt });

        var requestBody = new
        {
            model = model,
            messages = messages,
            stream = true
        };

        requestMessage.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new System.IO.StreamReader(stream);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.StartsWith("data: [DONE]", System.StringComparison.Ordinal)) break;

            if (line.StartsWith("data: ", System.StringComparison.Ordinal))
            {
                var json = line.Substring(6);
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var choice = choices[0];
                    string? chunkContent = null;
                    if (choice.TryGetProperty("delta", out var delta) && delta.TryGetProperty("content", out var contentElement))
                    {
                        chunkContent = contentElement.GetString();
                    }

                    string? finishReason = null;
                    if (choice.TryGetProperty("finish_reason", out var finishReasonElement) && finishReasonElement.ValueKind != JsonValueKind.Null)
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
