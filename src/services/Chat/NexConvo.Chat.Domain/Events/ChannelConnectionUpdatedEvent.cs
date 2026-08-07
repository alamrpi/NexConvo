using NexConvo.BuildingBlocks.Domain;
using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Domain.Events;

/// <summary>
/// Published after a ChannelConnection is saved or deactivated so downstream consumers
/// (SignalR hub, webhook router) can refresh their routing tables.
/// </summary>
public sealed record ChannelConnectionUpdatedEvent(
    Guid TenantId,
    Guid ConnectionId,
    ChatChannel Channel,
    string ExternalAccountId,
    bool IsActive) : IDomainEvent;
