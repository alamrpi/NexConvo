using FluentAssertions;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Application.Features.Conversations.Commands;
using NexConvo.Chat.Domain.Entities;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Chat.Domain.Exceptions;
using NexConvo.Chat.Domain.ValueObjects;
using NexConvo.Contracts.Enums;
using NSubstitute;

namespace NexConvo.Chat.Application.UnitTests.Features.Conversations.Commands;

public class TakeOverConversationCommandHandlerTests
{
    private static (TakeOverConversationCommandHandler Handler, IChatDbContext Db) BuildSut(
        Guid tenantId, List<Conversation> conversations)
    {
        var conversationsSet = conversations.AsQueryable().BuildMockDbSet();

        var db = Substitute.For<IChatDbContext>();
        db.Conversations.Returns(conversationsSet);
        db.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var tenant = Substitute.For<ITenantContext>();
        tenant.TenantId.Returns(tenantId);

        var handler = new TakeOverConversationCommandHandler(
            db, tenant, Substitute.For<IChatEventPublisher>(), Substitute.For<ILogger<TakeOverConversationCommandHandler>>());
        return (handler, db);
    }

    [Fact]
    public async Task Handle_PendingHumanConversation_TransitionsToHumanHandling_AndAssignsAgent()
    {
        var tenantId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var conversation = Conversation.StartAiHandling(tenantId, new ChannelIdentity(LeadSourceChannel.Web, "a"), null);
        conversation.RequestHandoff(EscalationReason.LowConfidence);

        var (handler, db) = BuildSut(tenantId, [conversation]);

        var result = await handler.Handle(
            new TakeOverConversationCommand(conversation.Id, agentId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        conversation.State.Should().Be(ConversationState.HumanHandling);
        conversation.AssignedAgentUserId.Should().Be(agentId);
        await db.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ConversationNotPendingHuman_RejectsTakeOver_AndLeavesStateUnchanged()
    {
        var tenantId = Guid.NewGuid();
        var conversation = Conversation.StartAiHandling(tenantId, new ChannelIdentity(LeadSourceChannel.Web, "a"), null);

        var (handler, db) = BuildSut(tenantId, [conversation]);

        var act = () => handler.Handle(
            new TakeOverConversationCommand(conversation.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidConversationStateTransitionException>();
        conversation.State.Should().Be(ConversationState.AiHandling);
        conversation.AssignedAgentUserId.Should().BeNull();
        await db.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ConversationNotFound_ReturnsNotFound()
    {
        var tenantId = Guid.NewGuid();
        var (handler, _) = BuildSut(tenantId, []);

        var result = await handler.Handle(
            new TakeOverConversationCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Status.Should().Be(ResultStatus.NotFound);
    }
}
