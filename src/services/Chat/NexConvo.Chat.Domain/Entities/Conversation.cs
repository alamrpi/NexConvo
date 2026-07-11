using NexConvo.BuildingBlocks.Domain;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Chat.Domain.Events;
using NexConvo.Chat.Domain.Exceptions;
using NexConvo.Chat.Domain.ValueObjects;

namespace NexConvo.Chat.Domain.Entities;

/// <summary>
/// Aggregate root for a single channel conversation thread. Owns the AI/human handoff
/// state machine: AiHandling -> PendingHuman -> HumanHandling -> Resolved -> (Reopen) AiHandling,
/// with Closed as a terminal state reachable from any point via <see cref="Close"/>.
/// </summary>
public class Conversation : BaseAggregateRoot
{
    public ChannelIdentity Channel { get; private set; } = null!;
    public ConversationState State { get; private set; }
    public Guid? ContactId { get; private set; }
    public Guid? AssignedAgentUserId { get; private set; }
    public string? LastInboundProviderMessageId { get; private set; }

    private Conversation()
    {
        // EF Core
    }

    public static Conversation StartAiHandling(Guid tenantId, ChannelIdentity channel, Guid? contactId)
    {
        var conversation = new Conversation
        {
            TenantId = tenantId,
            Channel = channel,
            ContactId = contactId,
            State = ConversationState.AiHandling,
        };
        conversation.CreatedAt = DateTimeOffset.UtcNow;
        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        return conversation;
    }

    public Message AppendInbound(string? providerMessageId, string body)
    {
        LastInboundProviderMessageId = providerMessageId;
        UpdatedAt = DateTimeOffset.UtcNow;
        return Message.Inbound(TenantId, Id, MessageSender.Contact(displayRef: null), providerMessageId, body);
    }

    public Message AppendAiReply(string text, RagConfidence confidence, int? tokens)
    {
        if (State != ConversationState.AiHandling)
        {
            throw new AiReplySuppressedException(
                EscalationReason.LowConfidence,
                $"Conversation {Id} state is {State}, expected AiHandling.");
        }

        UpdatedAt = DateTimeOffset.UtcNow;
        var message = Message.Outbound(TenantId, Id, MessageSender.Ai(), text, confidence.Score);
        RaiseDomainEvent(new AiReplyAppendedDomainEvent(Id, message.Id, confidence.Score));
        return message;
    }

    public Message AppendAgentReply(Guid agentUserId, string text)
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        return Message.Outbound(TenantId, Id, MessageSender.Agent(agentUserId, "Agent"), text, confidence: null);
    }

    public Escalation RequestHandoff(EscalationReason reason)
    {
        if (State is not (ConversationState.AiHandling or ConversationState.PendingHuman))
        {
            throw new InvalidConversationStateTransitionException(State, ConversationState.PendingHuman);
        }

        State = ConversationState.PendingHuman;
        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new ConversationHandoffRequestedDomainEvent(Id, reason));
        return Escalation.Raise(TenantId, Id, reason);
    }

    public void TakeOver(Guid agentUserId)
    {
        if (State != ConversationState.PendingHuman)
        {
            throw new InvalidConversationStateTransitionException(State, ConversationState.HumanHandling);
        }

        State = ConversationState.HumanHandling;
        AssignedAgentUserId = agentUserId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Resolve()
    {
        if (State is not (ConversationState.HumanHandling or ConversationState.AiHandling))
        {
            throw new InvalidConversationStateTransitionException(State, ConversationState.Resolved);
        }

        State = ConversationState.Resolved;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Reopen()
    {
        if (State is not (ConversationState.Resolved or ConversationState.Closed))
        {
            throw new InvalidConversationStateTransitionException(State, ConversationState.AiHandling);
        }

        State = ConversationState.AiHandling;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Close()
    {
        State = ConversationState.Closed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
