using FluentAssertions;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Application.Features.Conversations.Commands;
using NexConvo.Chat.Domain.Entities;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Chat.Domain.Exceptions;
using NexConvo.Chat.Domain.ValueObjects;
using NexConvo.Contracts.Enums;
using NSubstitute;

namespace NexConvo.Chat.Application.UnitTests.Features.Conversations.Commands;

public class SendAgentReplyCommandHandlerTests
{
    private static (SendAgentReplyCommandHandler Handler, IChatDbContext Db) BuildSut(
        Guid tenantId, List<Conversation> conversations)
    {
        var conversationsSet = conversations.AsQueryable().BuildMockDbSet();
        var messagesSet = new List<Message>().AsQueryable().BuildMockDbSet();

        var db = Substitute.For<IChatDbContext>();
        db.Conversations.Returns(conversationsSet);
        db.Messages.Returns(messagesSet);
        db.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var tenant = Substitute.For<ITenantContext>();
        tenant.TenantId.Returns(tenantId);

        var handler = new SendAgentReplyCommandHandler(
            db, tenant, Substitute.For<IChatEventPublisher>(), Substitute.For<ILogger<SendAgentReplyCommandHandler>>());
        return (handler, db);
    }

    [Fact]
    public async Task Handle_HappyPath_PersistsOutboundAgentMessage_AndRefreshesUpdatedAt()
    {
        var tenantId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var conversation = Conversation.StartAiHandling(tenantId, new ChannelIdentity(LeadSourceChannel.Web, "a"), null);
        conversation.RequestHandoff(EscalationReason.LowConfidence);
        conversation.TakeOver(agentId);
        var updatedAtBefore = conversation.UpdatedAt;

        var (handler, db) = BuildSut(tenantId, [conversation]);

        var result = await handler.Handle(
            new SendAgentReplyCommand(conversation.Id, agentId, "Sure, I can help with that."),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Body.Should().Be("Sure, I can help with that.");
        result.Value.SenderRole.Should().Be("Agent");
        db.Messages.Received(1).Add(Arg.Is<Message>(m =>
            m.Direction == MessageDirection.Outbound &&
            m.Body == "Sure, I can help with that." &&
            m.Sender.Role == MessageSenderRole.Agent));
        await db.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        conversation.UpdatedAt.Should().BeAfter(updatedAtBefore);
    }

    [Fact]
    public async Task Handle_ConversationResolved_RejectsReply_AndPersistsNoMessage()
    {
        var tenantId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var conversation = Conversation.StartAiHandling(tenantId, new ChannelIdentity(LeadSourceChannel.Web, "a"), null);
        conversation.Resolve();

        var (handler, db) = BuildSut(tenantId, [conversation]);

        var act = () => handler.Handle(
            new SendAgentReplyCommand(conversation.Id, agentId, "too late"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidConversationStateTransitionException>();
        db.Messages.DidNotReceive().Add(Arg.Any<Message>());
        await db.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ConversationClosed_RejectsReply()
    {
        var tenantId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var conversation = Conversation.StartAiHandling(tenantId, new ChannelIdentity(LeadSourceChannel.Web, "a"), null);
        conversation.Close();

        var (handler, db) = BuildSut(tenantId, [conversation]);

        var act = () => handler.Handle(
            new SendAgentReplyCommand(conversation.Id, agentId, "too late"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidConversationStateTransitionException>();
        db.Messages.DidNotReceive().Add(Arg.Any<Message>());
    }

    [Fact]
    public async Task Handle_ConversationNotFound_ReturnsNotFound()
    {
        var tenantId = Guid.NewGuid();
        var (handler, _) = BuildSut(tenantId, []);

        var result = await handler.Handle(
            new SendAgentReplyCommand(Guid.NewGuid(), Guid.NewGuid(), "hi"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Status.Should().Be(NexConvo.BuildingBlocks.Results.ResultStatus.NotFound);
    }
}
