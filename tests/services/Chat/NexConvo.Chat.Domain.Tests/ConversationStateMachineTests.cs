using FluentAssertions;
using NexConvo.Chat.Domain.Entities;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Chat.Domain.Exceptions;
using NexConvo.Chat.Domain.ValueObjects;
using NexConvo.Contracts.Enums;
using Xunit;

namespace NexConvo.Chat.Domain.Tests;

public class ConversationStateMachineTests
{
    private static Conversation NewConversation() =>
        Conversation.StartAiHandling(Guid.NewGuid(), new ChannelIdentity(LeadSourceChannel.WhatsApp, "ext-123"), contactId: null);

    [Fact]
    public void StartAiHandling_CreatesConversationInAiHandlingState()
    {
        var conversation = NewConversation();

        conversation.State.Should().Be(ConversationState.AiHandling);
    }

    [Fact]
    public void AppendInbound_RecordsMessageAndUpdatesLastProviderMessageId()
    {
        var conversation = NewConversation();

        var message = conversation.AppendInbound("provider-msg-1", "Hello, I need help.");

        message.Direction.Should().Be(MessageDirection.Inbound);
        message.Body.Should().Be("Hello, I need help.");
        conversation.LastInboundProviderMessageId.Should().Be("provider-msg-1");
    }

    [Fact]
    public void AppendAiReply_WhileAiHandling_Succeeds()
    {
        var conversation = NewConversation();

        var reply = conversation.AppendAiReply("Refunds take 5 business days.", RagConfidence.FromRetrievalAndAbstention(0.9, false), tokens: 42);

        reply.Direction.Should().Be(MessageDirection.Outbound);
        reply.Sender.Role.Should().Be(MessageSenderRole.Ai);
        reply.Confidence.Should().NotBeNull();
    }

    [Fact]
    public void AppendAiReply_WhilePendingHuman_ThrowsAiReplySuppressed()
    {
        var conversation = NewConversation();
        conversation.RequestHandoff(EscalationReason.LowConfidence);

        var act = () => conversation.AppendAiReply("Should not send.", RagConfidence.FromRetrievalAndAbstention(0.9, false), null);

        act.Should().Throw<AiReplySuppressedException>();
    }

    [Fact]
    public void AppendAiReply_WhileHumanHandling_ThrowsAiReplySuppressed()
    {
        var conversation = NewConversation();
        conversation.RequestHandoff(EscalationReason.LowConfidence);
        conversation.TakeOver(Guid.NewGuid());

        var act = () => conversation.AppendAiReply("Should not send.", RagConfidence.FromRetrievalAndAbstention(0.9, false), null);

        act.Should().Throw<AiReplySuppressedException>();
    }

    [Fact]
    public void RequestHandoff_FromAiHandling_TransitionsToPendingHuman()
    {
        var conversation = NewConversation();

        var escalation = conversation.RequestHandoff(EscalationReason.LowConfidence);

        conversation.State.Should().Be(ConversationState.PendingHuman);
        escalation.Reason.Should().Be(EscalationReason.LowConfidence);
    }

    [Fact]
    public void TakeOver_FromPendingHuman_TransitionsToHumanHandling()
    {
        var conversation = NewConversation();
        conversation.RequestHandoff(EscalationReason.LowConfidence);
        var agentId = Guid.NewGuid();

        conversation.TakeOver(agentId);

        conversation.State.Should().Be(ConversationState.HumanHandling);
        conversation.AssignedAgentUserId.Should().Be(agentId);
    }

    [Fact]
    public void TakeOver_FromAiHandling_ThrowsInvalidTransition()
    {
        var conversation = NewConversation();

        var act = () => conversation.TakeOver(Guid.NewGuid());

        act.Should().Throw<InvalidConversationStateTransitionException>();
    }

    [Fact]
    public void Resolve_FromClosed_ThrowsInvalidTransition()
    {
        var conversation = NewConversation();
        conversation.Close();

        var act = () => conversation.Resolve();

        act.Should().Throw<InvalidConversationStateTransitionException>();
    }

    [Fact]
    public void Reopen_FromResolved_TransitionsBackToAiHandling()
    {
        var conversation = NewConversation();
        conversation.Resolve();

        conversation.Reopen();

        conversation.State.Should().Be(ConversationState.AiHandling);
    }
}
