using NexConvo.Contracts.Enums;

namespace NexConvo.Contracts.Events.Chat;

/// <summary>
/// Published after a channel connection is created, updated, or deactivated.
/// Contains no secrets — only structural metadata safe for downstream consumers.
/// </summary>
public sealed record ChannelConnectionUpdatedEvent(
    Guid TenantId,
    Guid ChannelConnectionId,
    LeadSourceChannel Channel,
    string? ExternalAccountId,
    bool IsActive) : IntegrationEvent;
