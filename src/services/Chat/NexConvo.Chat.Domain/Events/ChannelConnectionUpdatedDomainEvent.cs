using NexConvo.BuildingBlocks.Domain;
using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Domain.Events;

public sealed record ChannelConnectionUpdatedDomainEvent(
    Guid ChannelConnectionId,
    Guid TenantId,
    ChatChannel Channel) : IDomainEvent;
