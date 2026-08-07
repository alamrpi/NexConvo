using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NexConvo.BuildingBlocks.Ai.Services;

namespace NexConvo.BuildingBlocks.Ai;

public static class DependencyInjection
{
    /// <summary>
    /// Adds a readiness check that probes the active embedding provider. Register from a host that
    /// has called <see cref="AddEmbeddingProviders"/>, e.g.
    /// <c>builder.Services.AddHealthChecks().AddEmbeddingProviderHealthCheck();</c>
    /// </summary>
    public static IHealthChecksBuilder AddEmbeddingProviderHealthCheck(
        this IHealthChecksBuilder builder, string name = "embedding-provider")
    {
        builder.Services.AddHttpClient<EmbeddingProviderHealthCheck>();
        return builder.AddCheck<EmbeddingProviderHealthCheck>(name, tags: new[] { "ready" });
    }

    /// <summary>
    /// Registers the swappable embedding providers (BGE-M3 default, Cohere alternative), each with a
    /// resilient typed <see cref="HttpClient"/>, plus the factory that selects the active one from
    /// <c>EMBEDDING:PROVIDER</c>. Mirrors <see cref="AddAiProviders"/>.
    /// </summary>
    public static IServiceCollection AddEmbeddingProviders(this IServiceCollection services, IConfiguration configuration)
    {
        // Ensure the providers/factory can always resolve configuration (EMBEDDING__*). In an ASP.NET
        // host IConfiguration is already registered, so this is a harmless no-op there.
        services.TryAddSingleton(configuration);

        services.AddHttpClient<BgeM3EmbeddingProviderService>().AddStandardResilienceHandler();
        services.AddHttpClient<CohereEmbeddingProviderService>().AddStandardResilienceHandler();

        services.AddKeyedTransient<IEmbeddingProviderService, BgeM3EmbeddingProviderService>("BgeM3");
        services.AddKeyedTransient<IEmbeddingProviderService, CohereEmbeddingProviderService>("Cohere");

        services.AddSingleton<IEmbeddingProviderFactory, EmbeddingProviderFactory>();

        // Available to consumers for tenant-attributed token accounting; the stateless providers here
        // deliberately don't invoke it (they carry no tenant context) — mirrors AddAiProviders.
        services.TryAddScoped<ITokenUsageLogger, TokenUsageLogger>();

        return services;
    }

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
