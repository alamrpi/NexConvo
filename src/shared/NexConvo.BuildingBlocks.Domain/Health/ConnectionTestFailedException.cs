namespace NexConvo.BuildingBlocks.Domain.Health;

/// <summary>
/// Thrown when a server-side re-test of new/changed integration credentials fails during a
/// save/upsert flow (e.g. S3 config). Mapped to HTTP 422 by the shared exception middleware —
/// distinct from <see cref="ConnectionUnhealthyException"/> (409), which guards USE of an
/// already-saved-but-unhealthy integration rather than a save-time credential re-test.
/// </summary>
public sealed class ConnectionTestFailedException(string integrationKind, string? detail)
    : Exception($"The {integrationKind} connection test failed. {detail}".Trim())
{
    public string IntegrationKind { get; } = integrationKind;
}
