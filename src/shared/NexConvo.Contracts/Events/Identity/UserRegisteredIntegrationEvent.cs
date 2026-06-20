namespace NexConvo.Contracts.Events.Identity;

/// <summary>
/// Published by the Identity service when a user is registered within a tenant.
/// Consumers can mirror a thin user read-model.
/// </summary>
public sealed record UserRegisteredIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }
    public required Guid TenantId { get; init; }
    public required string Email { get; init; }
    public required string FullName { get; init; }
}
