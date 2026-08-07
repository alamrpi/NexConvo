using FluentAssertions;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Application.Features.Conversations.Queries;
using NexConvo.Chat.Domain.Entities;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Chat.Domain.ValueObjects;
using NexConvo.Contracts.Enums;
using NSubstitute;

namespace NexConvo.Chat.Application.UnitTests.Features.Conversations.Queries;

public class GetConversationsQueryHandlerTests
{
    private static (GetConversationsQueryHandler Handler, IChatDbContext Db) BuildSut(
        Guid tenantId, List<Conversation> conversations, List<Message>? messages = null)
    {
        var conversationsSet = conversations.AsQueryable().BuildMockDbSet();
        var messagesSet = (messages ?? []).AsQueryable().BuildMockDbSet();

        var db = Substitute.For<IChatDbContext>();
        db.Conversations.Returns(conversationsSet);
        db.Messages.Returns(messagesSet);

        var tenant = Substitute.For<ITenantContext>();
        tenant.TenantId.Returns(tenantId);

        var handler = new GetConversationsQueryHandler(
            db, tenant, Substitute.For<ILogger<GetConversationsQueryHandler>>());
        return (handler, db);
    }

    [Fact]
    public async Task Handle_NoFilters_ReturnsOnlyCurrentTenantConversations_OrderedByMostRecentActivity()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();

        var older = Conversation.StartAiHandling(tenantId, new ChannelIdentity(LeadSourceChannel.WhatsApp, "a"), null);
        var newer = Conversation.StartAiHandling(tenantId, new ChannelIdentity(LeadSourceChannel.Web, "b"), null);
        typeof(Conversation).GetProperty(nameof(Conversation.UpdatedAt))!.SetValue(older, DateTimeOffset.UtcNow.AddMinutes(-10));
        typeof(Conversation).GetProperty(nameof(Conversation.UpdatedAt))!.SetValue(newer, DateTimeOffset.UtcNow);

        var otherTenantConvo = Conversation.StartAiHandling(otherTenantId, new ChannelIdentity(LeadSourceChannel.WhatsApp, "c"), null);

        var (handler, _) = BuildSut(tenantId, [older, newer, otherTenantConvo]);

        var result = await handler.Handle(
            new GetConversationsQuery(null, null, false, Guid.NewGuid(), null), CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.Items.Select(c => c.Id).Should().ContainInOrder(newer.Id, older.Id);
        result.Items.Should().NotContain(c => c.Id == otherTenantConvo.Id);
    }

    [Fact]
    public async Task Handle_StateFilter_ReturnsOnlyMatchingState()
    {
        var tenantId = Guid.NewGuid();
        var pending = Conversation.StartAiHandling(tenantId, new ChannelIdentity(LeadSourceChannel.WhatsApp, "a"), null);
        pending.RequestHandoff(EscalationReason.LowConfidence);
        var aiHandling = Conversation.StartAiHandling(tenantId, new ChannelIdentity(LeadSourceChannel.Web, "b"), null);

        var (handler, _) = BuildSut(tenantId, [pending, aiHandling]);

        var result = await handler.Handle(
            new GetConversationsQuery(ConversationState.PendingHuman, null, false, Guid.NewGuid(), null),
            CancellationToken.None);

        result.Items.Should().ContainSingle(c => c.Id == pending.Id);
    }

    [Fact]
    public async Task Handle_AssignedToMe_ReturnsOnlyConversationsAssignedToRequestingAgent()
    {
        var tenantId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var mine = Conversation.StartAiHandling(tenantId, new ChannelIdentity(LeadSourceChannel.WhatsApp, "a"), null);
        mine.RequestHandoff(EscalationReason.LowConfidence);
        mine.TakeOver(agentId);

        var someoneElses = Conversation.StartAiHandling(tenantId, new ChannelIdentity(LeadSourceChannel.Web, "b"), null);
        someoneElses.RequestHandoff(EscalationReason.LowConfidence);
        someoneElses.TakeOver(Guid.NewGuid());

        var (handler, _) = BuildSut(tenantId, [mine, someoneElses]);

        var result = await handler.Handle(
            new GetConversationsQuery(null, null, AssignedToMe: true, agentId, null), CancellationToken.None);

        result.Items.Should().ContainSingle(c => c.Id == mine.Id);
    }

    [Fact]
    public async Task Handle_ReturnsLastMessagePreviewFromMostRecentMessage()
    {
        var tenantId = Guid.NewGuid();
        var conversation = Conversation.StartAiHandling(tenantId, new ChannelIdentity(LeadSourceChannel.Web, "a"), null);
        var first = conversation.AppendInbound("p1", "first message");
        var second = conversation.AppendAiReply("second (latest) message", RagConfidence.FromRetrievalAndAbstention(0.9, false), null);

        var (handler, _) = BuildSut(tenantId, [conversation], [first, second]);

        var result = await handler.Handle(
            new GetConversationsQuery(null, null, false, Guid.NewGuid(), null), CancellationToken.None);

        var dto = result.Items.Should().ContainSingle().Subject;
        dto.LastMessagePreview.Should().Be("second (latest) message");
        dto.LastMessageFromAi.Should().BeTrue();
    }
}
