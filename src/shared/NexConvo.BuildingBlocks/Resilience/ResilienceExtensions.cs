using Microsoft.Extensions.DependencyInjection;

namespace NexConvo.BuildingBlocks.Resilience;

public static class ResilienceExtensions
{
    /// <summary>
    /// Applies the standard Polly resilience pipeline (retry with backoff + jitter, circuit
    /// breaker, timeout) to a typed HTTP client. Every external API client uses this
    /// (skill Standard 8). Lives in Infrastructure wiring, never in Domain/Application.
    /// </summary>
    public static IHttpClientBuilder AddNexConvoResilience(this IHttpClientBuilder builder)
    {
        builder.AddStandardResilienceHandler();
        return builder;
    }
}
