using NexConvo.BuildingBlocks.Domain;
using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Domain.Events;

public sealed record ConversationHandoffRequestedDomainEvent(Guid ConversationId, EscalationReason Reason) : IDomainEvent;
