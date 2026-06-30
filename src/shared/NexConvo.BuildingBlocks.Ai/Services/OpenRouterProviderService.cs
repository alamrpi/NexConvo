using System.Net.Http;

namespace NexConvo.BuildingBlocks.Ai.Services;

public class OpenRouterProviderService(HttpClient httpClient) : OpenAiCompatibleProviderService(httpClient)
{
    protected override string DefaultEndpoint => "https://openrouter.ai/api/v1/chat/completions";

    protected override void AddProviderHeaders(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation("HTTP-Referer", "https://nexconvo.app");
        request.Headers.TryAddWithoutValidation("X-Title", "NexConvo");
    }
}
