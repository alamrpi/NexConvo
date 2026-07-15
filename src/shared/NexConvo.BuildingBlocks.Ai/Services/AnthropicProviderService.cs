using NexConvo.BuildingBlocks.Ai.Models;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace NexConvo.BuildingBlocks.Ai.Services;

public class AnthropicProviderService : IAiProviderService
{
    private readonly HttpClient _httpClient;

    public AnthropicProviderService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async IAsyncEnumerable<AiStreamChunk> GenerateStreamAsync(
        string prompt, 
        string? systemPrompt, 
        string apiKey, 
        string model, 
        string? baseUrl = null, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var requestUrl = baseUrl ?? "https://api.anthropic.com/v1/messages";

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        requestMessage.Headers.Add("x-api-key", apiKey);
        requestMessage.Headers.Add("anthropic-version", "2023-06-01");

        var requestBody = new
        {
            model = model,
            system = systemPrompt,
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            stream = true,
            max_tokens = 4096
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
            
            if (line.StartsWith("data: ", System.StringComparison.Ordinal))
            {
                var json = line.Substring(6);
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (root.TryGetProperty("type", out var typeElement))
                {
                    var type = typeElement.GetString();
                    if (type == "content_block_delta")
                    {
                        if (root.TryGetProperty("delta", out var delta) && delta.TryGetProperty("text", out var textElement))
                        {
                            yield return new AiStreamChunk(textElement.GetString() ?? string.Empty, null, null, null);
                        }
                    }
                    else if (type == "message_stop")
                    {
                        yield return new AiStreamChunk(string.Empty, "stop", null, null);
                    }
                }
            }
        }
    }

    public async Task<IReadOnlyList<ModelDto>> GetAvailableModelsAsync(string apiKey, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        var requestUrl = baseUrl ?? "https://api.anthropic.com/v1/models";
        // Ensure it points to /models if baseUrl is provided for something else
        if (requestUrl.EndsWith("/messages", System.StringComparison.OrdinalIgnoreCase))
        {
            requestUrl = requestUrl[..^"/messages".Length] + "/models";
        }

        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        requestMessage.Headers.Add("x-api-key", apiKey);
        requestMessage.Headers.Add("anthropic-version", "2023-06-01");

        try
        {
            using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // Fallback for Anthropic if the models endpoint is inaccessible
                return new List<ModelDto>
                {
                    new ModelDto("claude-3-5-sonnet-20241022", "Claude 3.5 Sonnet"),
                    new ModelDto("claude-3-5-haiku-20241022", "Claude 3.5 Haiku"),
                    new ModelDto("claude-3-opus-20240229", "Claude 3 Opus"),
                    new ModelDto("claude-3-haiku-20240307", "Claude 3 Haiku")
                };
            }

            var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken);

            var root = document.RootElement;
            var models = new List<ModelDto>();

            if (root.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in dataElement.EnumerateArray())
                {
                    if (item.TryGetProperty("id", out var idElement))
                    {
                        var id = idElement.GetString();
                        if (!string.IsNullOrEmpty(id))
                        {
                            var name = item.TryGetProperty("display_name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
                                ? nameElement.GetString()!
                                : id;
                            models.Add(new ModelDto(id, name));
                        }
                    }
                }
            }

            return models;
        }
        catch
        {
            return new List<ModelDto>
            {
                new ModelDto("claude-3-5-sonnet-20241022", "Claude 3.5 Sonnet"),
                new ModelDto("claude-3-haiku-20240307", "Claude 3 Haiku")
            };
        }
    }
}
