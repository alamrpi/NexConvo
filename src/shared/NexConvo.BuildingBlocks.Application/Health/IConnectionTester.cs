using NexConvo.BuildingBlocks.Domain.Health;

namespace NexConvo.BuildingBlocks.Application.Health;

/// <summary>
/// Orchestration contract for testing a stored integration credential (S3, AI provider, channel,
/// SMS/email gateway, etc.) against the live third-party endpoint. Implemented in each service's
/// Infrastructure layer (Standard 8: wrapped in Polly) and invoked from Application handlers for
/// the "Test Connection" and scheduled re-test flows (Standard 22).
/// </summary>
public interface IConnectionTester<TInput>
{
    /// <summary>Stable discriminator for the integration this tester targets, e.g. "s3", "openrouter".</summary>
    string IntegrationKind { get; }

    Task<ConnectionHealth> TestAsync(TInput input, CancellationToken ct);
}
