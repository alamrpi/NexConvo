using System.Net.Http;

namespace NexConvo.BuildingBlocks.Ai.Services;

public class OpenAiProviderService(HttpClient httpClient) : OpenAiCompatibleProviderService(httpClient)
{
    protected override string DefaultEndpoint => "https://api.openai.com/v1/chat/completions";
}
