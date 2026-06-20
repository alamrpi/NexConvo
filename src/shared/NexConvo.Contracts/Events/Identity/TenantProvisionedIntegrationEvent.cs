namespace NexConvo.Contracts.Events.Identity;

/// <summary>
/// Published by the Identity service when a new tenant (and its owner user) is provisioned.
/// Consumers (e.g. CRM, Automation) bootstrap tenant-local defaults.
/// </summary>
public sealed record TenantProvisionedIntegrationEvent : IntegrationEvent
{
    public required Guid TenantId { get; init; }
    public required string TenantName { get; init; }
    public required string TenantSlug { get; init; }
    public required Guid OwnerUserId { get; init; }
}
