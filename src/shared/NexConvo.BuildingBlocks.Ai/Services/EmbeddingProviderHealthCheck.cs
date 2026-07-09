using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NexConvo.Contracts.Enums;

namespace NexConvo.BuildingBlocks.Ai.Services;

/// <summary>
/// Readiness probe for the active embedding provider, so an unreachable embedder surfaces at
/// <c>/health/ready</c> instead of a silent 500 at ingestion/query time. For BGE-M3 it pings the
/// self-hosted Infinity server's <c>/health</c>; for the managed Cohere provider it verifies the
/// API key is configured.
/// </summary>
public class EmbeddingProviderHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public EmbeddingProviderHealthCheck(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var provider = _configuration["EMBEDDING:PROVIDER"];
        if (string.IsNullOrWhiteSpace(provider))
        {
            provider = nameof(EmbeddingProviderType.BgeM3);
        }

        if (string.Equals(provider, nameof(EmbeddingProviderType.Cohere), StringComparison.OrdinalIgnoreCase))
        {
            var apiKey = _configuration["EMBEDDING:COHERE:APIKEY"];
            return string.IsNullOrWhiteSpace(apiKey)
                ? HealthCheckResult.Degraded("Cohere embedding API key is not configured.")
                : HealthCheckResult.Healthy("Cohere embedding provider is configured.");
        }

        // BGE-M3 (default): probe the self-hosted Infinity server so an outage is visible up front.
        var baseUrl = _configuration["EMBEDDING:BGEM3:BASEURL"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = "http://bge-m3:7997";
        }

        try
        {
            using var response = await _httpClient.GetAsync($"{baseUrl}/health", cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("BGE-M3 embedding server is reachable.")
                : HealthCheckResult.Unhealthy($"BGE-M3 embedding server returned HTTP {(int)response.StatusCode}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("BGE-M3 embedding server is unreachable.", ex);
        }
    }
}
