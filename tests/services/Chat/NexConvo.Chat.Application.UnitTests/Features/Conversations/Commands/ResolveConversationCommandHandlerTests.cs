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

public class ResolveConversationCommandHandlerTests
{
    private static (ResolveConversationCommandHandler Handler, IChatDbContext Db) BuildSut(
        Guid tenantId, List<Conversation> conversations)
    {
        var conversationsSet = conversations.AsQueryable().BuildMockDbSet();

        var db = Substitute.For<IChatDbContext>();
        db.Conversations.Returns(conversationsSet);
        db.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var tenant = Substitute.For<ITenantContext>();
        tenant.TenantId.Returns(tenantId);

        var handler = new ResolveConversationCommandHandler(
            db, tenant, Substitute.For<IChatEventPublisher>(), Substitute.For<ILogger<ResolveConversationCommandHandler>>());
        return (handler, db);
    }

    [Theory]
    [InlineData(true)] // HumanHandling
    [InlineData(false)] // AiHandling
    public async Task Handle_HumanHandlingOrAiHandling_TransitionsToResolved(bool viaHumanHandling)
    {
        var tenantId = Guid.NewGuid();
        var conversation = Conversation.StartAiHandling(tenantId, new ChannelIdentity(LeadSourceChannel.Web, "a"), null);
        if (viaHumanHandling)
        {
            conversation.RequestHandoff(EscalationReason.LowConfidence);
            conversation.TakeOver(Guid.NewGuid());
        }

        var (handler, db) = BuildSut(tenantId, [conversation]);

        var result = await handler.Handle(
            new ResolveConversationCommand(conversation.Id, Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        conversation.State.Should().Be(ConversationState.Resolved);
        await db.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ConversationPendingHuman_RejectsResolve()
    {
        var tenantId = Guid.NewGuid();
        var conversation = Conversation.StartAiHandling(tenantId, new ChannelIdentity(LeadSourceChannel.Web, "a"), null);
        conversation.RequestHandoff(EscalationReason.LowConfidence);

        var (handler, db) = BuildSut(tenantId, [conversation]);

        var act = () => handler.Handle(
            new ResolveConversationCommand(conversation.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidConversationStateTransitionException>();
        conversation.State.Should().Be(ConversationState.PendingHuman);
        await db.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ConversationNotFound_ReturnsNotFound()
    {
        var tenantId = Guid.NewGuid();
        var (handler, _) = BuildSut(tenantId, []);

        var result = await handler.Handle(
            new ResolveConversationCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Status.Should().Be(ResultStatus.NotFound);
    }
}
