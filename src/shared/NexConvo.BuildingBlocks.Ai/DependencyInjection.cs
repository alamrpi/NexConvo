using Microsoft.Extensions.DependencyInjection;
using NexConvo.BuildingBlocks.Ai.Services;

namespace NexConvo.BuildingBlocks.Ai;

public static class DependencyInjection
{
    public static IServiceCollection AddAiProviders(this IServiceCollection services)
    {
        services.AddHttpClient<OpenAiProviderService>().AddStandardResilienceHandler();
        services.AddHttpClient<AnthropicProviderService>().AddStandardResilienceHandler();
        services.AddHttpClient<GeminiProviderService>().AddStandardResilienceHandler();
        services.AddHttpClient<OpenRouterProviderService>().AddStandardResilienceHandler();
        services.AddHttpClient<DeepSeekProviderService>().AddStandardResilienceHandler();

        services.AddKeyedTransient<IAiProviderService, OpenAiProviderService>("OpenAI");
        services.AddKeyedTransient<IAiProviderService, AnthropicProviderService>("Anthropic");
        services.AddKeyedTransient<IAiProviderService, GeminiProviderService>("Gemini");
        services.AddKeyedTransient<IAiProviderService, OpenRouterProviderService>("OpenRouter");
        services.AddKeyedTransient<IAiProviderService, DeepSeekProviderService>("DeepSeek");

        services.AddSingleton<IAiProviderFactory, AiProviderFactory>();
        services.AddScoped<ITokenUsageLogger, TokenUsageLogger>();

        return services;
    }
}
