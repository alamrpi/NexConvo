using DomainConnectionHealth = NexConvo.BuildingBlocks.Domain.Health.ConnectionHealth;

namespace NexConvo.BuildingBlocks.Application.ConnectionHealth;

/// <summary>
/// Orchestration contract for testing a stored integration credential (S3, AI provider, channel,
/// SMS/email gateway, etc.) against the live third-party endpoint. Implemented in each service's
/// Infrastructure layer (Standard 8: wrapped in Polly) and invoked from Application handlers for
/// the "Test Connection" and scheduled re-test flows (Standard 22).
/// </summary>
/// <remarks>
/// Note the namespace here is <c>NexConvo.BuildingBlocks.Application.ConnectionHealth</c>, which
/// shares its last segment with the <c>ConnectionHealth</c> record in
/// <c>NexConvo.BuildingBlocks.Domain.Health</c>. A plain <c>using</c> makes the type name ambiguous
/// with the namespace segment (CS0118), so it is referenced via an alias.
/// </remarks>
public interface IConnectionTester<TInput>
{
    /// <summary>Stable discriminator for the integration this tester targets, e.g. "s3", "openrouter".</summary>
    string IntegrationKind { get; }

    Task<DomainConnectionHealth> TestAsync(TInput input, CancellationToken ct);
}
