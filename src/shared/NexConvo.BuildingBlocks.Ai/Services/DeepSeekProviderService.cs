using System.Net.Http;

namespace NexConvo.BuildingBlocks.Ai.Services;

public class DeepSeekProviderService(HttpClient httpClient) : OpenAiCompatibleProviderService(httpClient)
{
    protected override string DefaultEndpoint => "https://api.deepseek.com/v1/chat/completions";
}
