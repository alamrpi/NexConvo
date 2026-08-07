namespace NexConvo.BuildingBlocks.Domain.Health;

/// <summary>
/// Thrown by a config entity's EnsureHealthy() guard (Standard 22c) when a dependent feature tries
/// to use an integration whose last recorded <see cref="ConnectionStatus"/> is not Healthy. Caught
/// at the API edge and mapped to 409 Conflict — distinct from a validation failure, this means the
/// credentials are recognized but the live connection is not currently usable.
/// </summary>
public sealed class ConnectionUnhealthyException(string integrationKind, string? detail)
    : Exception($"The {integrationKind} connection is not healthy. {detail}".Trim())
{
    public string IntegrationKind { get; } = integrationKind;
}
