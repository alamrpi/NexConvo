using FluentAssertions;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Application.Features.Conversations.Queries;
using NexConvo.Chat.Domain.Entities;
using NexConvo.Chat.Domain.ValueObjects;
using NexConvo.Contracts.Enums;
using NSubstitute;

namespace NexConvo.Chat.Application.UnitTests.Features.Conversations.Queries;

public class GetConversationMessagesQueryHandlerTests
{
    private static (GetConversationMessagesQueryHandler Handler, IChatDbContext Db) BuildSut(
        Guid tenantId, List<Conversation> conversations, List<Message>? messages = null)
    {
        var conversationsSet = conversations.AsQueryable().BuildMockDbSet();
        var messagesSet = (messages ?? []).AsQueryable().BuildMockDbSet();

        var db = Substitute.For<IChatDbContext>();
        db.Conversations.Returns(conversationsSet);
        db.Messages.Returns(messagesSet);

        var tenant = Substitute.For<ITenantContext>();
        tenant.TenantId.Returns(tenantId);

        var handler = new GetConversationMessagesQueryHandler(
            db, tenant, Substitute.For<ILogger<GetConversationMessagesQueryHandler>>());
        return (handler, db);
    }

    [Fact]
    public async Task Handle_AssignedAgent_ReturnsMessagesInChronologicalOrder()
    {
        var tenantId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var conversation = Conversation.StartAiHandling(tenantId, new ChannelIdentity(LeadSourceChannel.Web, "a"), null);
        conversation.RequestHandoff(Domain.Enums.EscalationReason.LowConfidence);
        conversation.TakeOver(agentId);

        var m1 = conversation.AppendInbound("p1", "hello");
        var m2 = conversation.AppendAgentReply(agentId, "hi there");

        var (handler, _) = BuildSut(tenantId, [conversation], [m2, m1]); // stored out of order

        var result = await handler.Handle(
            new GetConversationMessagesQuery(conversation.Id, agentId, RequestingAgentHasReadPermission: false, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Select(m => m.Id).Should().ContainInOrder(m1.Id, m2.Id);
    }

    [Fact]
    public async Task Handle_AgentWithReadPermission_ButNotAssigned_CanStillView()
    {
        var tenantId = Guid.NewGuid();
        var conversation = Conversation.StartAiHandling(tenantId, new ChannelIdentity(LeadSourceChannel.Web, "a"), null);
        var m1 = conversation.AppendInbound("p1", "hello");

        var (handler, _) = BuildSut(tenantId, [conversation], [m1]);

        var result = await handler.Handle(
            new GetConversationMessagesQuery(conversation.Id, Guid.NewGuid(), RequestingAgentHasReadPermission: true, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().ContainSingle(m => m.Id == m1.Id);
    }

    [Fact]
    public async Task Handle_AgentWithoutAccess_ReturnsNotFound_SameAsNonexistentConversation()
    {
        var tenantId = Guid.NewGuid();
        var conversation = Conversation.StartAiHandling(tenantId, new ChannelIdentity(LeadSourceChannel.Web, "a"), null);

        var (handler, _) = BuildSut(tenantId, [conversation]);

        var resultForRealConversation = await handler.Handle(
            new GetConversationMessagesQuery(conversation.Id, Guid.NewGuid(), RequestingAgentHasReadPermission: false, null),
            CancellationToken.None);

        var resultForFakeConversation = await handler.Handle(
            new GetConversationMessagesQuery(Guid.NewGuid(), Guid.NewGuid(), RequestingAgentHasReadPermission: false, null),
            CancellationToken.None);

        resultForRealConversation.IsFailure.Should().BeTrue();
        resultForRealConversation.Status.Should().Be(ResultStatus.NotFound);
        resultForFakeConversation.IsFailure.Should().BeTrue();
        resultForFakeConversation.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Handle_ConversationInAnotherTenant_ReturnsNotFound()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var conversation = Conversation.StartAiHandling(otherTenantId, new ChannelIdentity(LeadSourceChannel.Web, "a"), null);

        var (handler, _) = BuildSut(tenantId, [conversation]);

        var result = await handler.Handle(
            new GetConversationMessagesQuery(conversation.Id, Guid.NewGuid(), RequestingAgentHasReadPermission: true, null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Status.Should().Be(ResultStatus.NotFound);
    }
}
