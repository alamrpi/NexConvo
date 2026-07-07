using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Ai.Services;
using NexConvo.BuildingBlocks.Application.Health;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.Integrations.Application.Features.AiConfig;

namespace NexConvo.Integrations.Infrastructure.ExternalServices;

/// <summary>
/// Live reachability+auth probe for an AI provider (Standard 22: test-then-save). Mirrors the
/// inline probe formerly in TestAiConnectionCommandHandler: requests a single streamed chunk for
/// a trivial prompt and treats the first chunk as proof of connectivity + valid credentials.
/// Never logs or returns the API key or the raw upstream exception message (Standard 13) — only a
/// sanitized, generic error is surfaced to callers.
/// </summary>
internal sealed class AiConnectionTester : IConnectionTester<AiTestInput>
{
    private readonly IAiProviderFactory _providerFactory;
    private readonly ILogger<AiConnectionTester>? _logger;

    public AiConnectionTester(IAiProviderFactory providerFactory, ILogger<AiConnectionTester>? logger = null)
    {
        _providerFactory = providerFactory;
        _logger = logger;
    }

    public string IntegrationKind => "ai";

    public async Task<ConnectionHealth> TestAsync(AiTestInput input, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var provider = _providerFactory.GetProvider(input.Provider);

            // Consume one chunk — enough to confirm auth + connectivity without burning tokens.
            await foreach (var _ in provider.GenerateStreamAsync(
                "Say OK",
                null,
                input.ApiKey,
                input.Model,
                string.IsNullOrWhiteSpace(input.BaseUrl) ? null : input.BaseUrl,
                ct))
            {
                break;
            }

            return ConnectionHealth.Healthy("Provider reachable", (int)sw.ElapsedMilliseconds);
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogWarning("AI connection test failed for {Provider}: {ExceptionType}", input.Provider, ex.GetType().Name);
            return ConnectionHealth.Failed(
                "Provider connection failed. Check the API key, model, and base URL.", (int)sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("AI connection test failed for {Provider}: {ExceptionType}", input.Provider, ex.GetType().Name);
            return ConnectionHealth.Failed("Connection failed. Check the API key and model name.", (int)sw.ElapsedMilliseconds);
        }
    }
}
