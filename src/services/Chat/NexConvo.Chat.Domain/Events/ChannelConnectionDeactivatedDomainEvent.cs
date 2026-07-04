using NexConvo.BuildingBlocks.Domain;
using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Domain.Events;

public sealed record ChannelConnectionDeactivatedDomainEvent(
    Guid ChannelConnectionId,
    Guid TenantId,
    ChatChannel Channel) : IDomainEvent;
