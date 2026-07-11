using NexConvo.BuildingBlocks.Domain;

namespace NexConvo.Chat.Domain.Events;

public sealed record AiReplyAppendedDomainEvent(Guid ConversationId, Guid MessageId, double ConfidenceScore) : IDomainEvent;
