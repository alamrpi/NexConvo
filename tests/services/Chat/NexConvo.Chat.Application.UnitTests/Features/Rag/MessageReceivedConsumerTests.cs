using FluentAssertions;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Application.Features.Rag.Commands;
using NexConvo.Chat.Application.Features.Rag.EventHandlers;
using NexConvo.Chat.Domain.Entities;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Chat.Domain.ValueObjects;
using NexConvo.Contracts.Enums;
using NexConvo.Contracts.Events.Chat;
using NSubstitute;

namespace NexConvo.Chat.Application.UnitTests.Features.Rag;

public class MessageReceivedConsumerTests
{
    private static IChatDbContext BuildDb(List<Conversation> conversations)
    {
        var conversationsSet = conversations.AsQueryable().BuildMockDbSet();
        var messagesSet = new List<Message>().AsQueryable().BuildMockDbSet();

        var db = Substitute.For<IChatDbContext>();
        db.Conversations.Returns(conversationsSet);
        db.Messages.Returns(messagesSet);
        db.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        return db;
    }

    [Fact]
    public async Task Consume_NewConversation_CreatesConversationAndDispatchesRagReplyCommand()
    {
        var tenantId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        var db = BuildDb([]);

        var sender = Substitute.For<ISender>();
        var dbFactory = Substitute.For<IChatDbContextFactory>();
        dbFactory.CreateForTenant(tenantId).Returns(db);
        var consumer = new MessageReceivedConsumer(dbFactory, sender, Substitute.For<ILogger<MessageReceivedConsumer>>());
        var message = new MessageReceivedIntegrationEvent
        {
            ConversationId = conversationId,
            TenantId = tenantId,
            Channel = LeadSourceChannel.WhatsApp,
            ExternalSenderId = "sender-1",
            MessageRef = "ref-1",
            Body = "How long do refunds take?",
            ProviderMessageId = "provider-1",
        };
        var context = Substitute.For<ConsumeContext<MessageReceivedIntegrationEvent>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);

        await consumer.Consume(context);

        await db.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await sender.Received(1).Send(Arg.Is<GenerateRagReplyCommand>(c => c.TenantId == tenantId && c.ConversationId != Guid.Empty), Arg.Any<CancellationToken>());
        db.Conversations.Received(1).Add(Arg.Is<Conversation>(c =>
            c.TenantId == tenantId &&
            c.Channel.Channel == LeadSourceChannel.WhatsApp &&
            c.Channel.ExternalConversationId == "sender-1"));
        db.Messages.Received(1).Add(Arg.Is<Message>(m =>
            m.Direction == MessageDirection.Inbound &&
            m.Body == "How long do refunds take?" &&
            m.ProviderMessageId == "provider-1"));
    }

    [Fact]
    public async Task Consume_ExistingConversationForSameChannelThread_ReusesConversationInsteadOfCreatingNew()
    {
        var tenantId = Guid.NewGuid();
        var existing = Conversation.StartAiHandling(
            tenantId, new ChannelIdentity(LeadSourceChannel.WhatsApp, "sender-1"), contactId: null);

        var db = BuildDb([existing]);

        var sender = Substitute.For<ISender>();
        var dbFactory = Substitute.For<IChatDbContextFactory>();
        dbFactory.CreateForTenant(tenantId).Returns(db);
        var consumer = new MessageReceivedConsumer(dbFactory, sender, Substitute.For<ILogger<MessageReceivedConsumer>>());

        var message = new MessageReceivedIntegrationEvent
        {
            ConversationId = Guid.NewGuid(),
            TenantId = tenantId,
            Channel = LeadSourceChannel.WhatsApp,
            ExternalSenderId = "sender-1",
            MessageRef = "ref-2",
            Body = "Follow-up question.",
            ProviderMessageId = "provider-2",
        };
        var context = Substitute.For<ConsumeContext<MessageReceivedIntegrationEvent>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);

        await consumer.Consume(context);

        db.Conversations.DidNotReceive().Add(Arg.Any<Conversation>());
        await sender.Received(1).Send(Arg.Is<GenerateRagReplyCommand>(c => c.ConversationId == existing.Id), Arg.Any<CancellationToken>());
        existing.LastInboundProviderMessageId.Should().Be("provider-2");
        db.Messages.Received(1).Add(Arg.Is<Message>(m => m.ProviderMessageId == "provider-2"));
    }

    [Fact]
    public async Task Consume_DifferentChannelSameTenant_CreatesSeparateConversation()
    {
        var tenantId = Guid.NewGuid();
        var whatsAppConversation = Conversation.StartAiHandling(
            tenantId, new ChannelIdentity(LeadSourceChannel.WhatsApp, "sender-1"), contactId: null);

        var db = BuildDb([whatsAppConversation]);

        var sender = Substitute.For<ISender>();
        var dbFactory = Substitute.For<IChatDbContextFactory>();
        dbFactory.CreateForTenant(tenantId).Returns(db);
        var consumer = new MessageReceivedConsumer(dbFactory, sender, Substitute.For<ILogger<MessageReceivedConsumer>>());

        var message = new MessageReceivedIntegrationEvent
        {
            ConversationId = Guid.NewGuid(),
            TenantId = tenantId,
            Channel = LeadSourceChannel.Facebook,
            ExternalSenderId = "sender-1",
            MessageRef = "ref-3",
            Body = "A message on a different channel.",
            ProviderMessageId = "provider-3",
        };
        var context = Substitute.For<ConsumeContext<MessageReceivedIntegrationEvent>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);

        await consumer.Consume(context);

        db.Conversations.Received(1).Add(Arg.Is<Conversation>(c => c.Channel.Channel == LeadSourceChannel.Facebook));
        db.Messages.Received(1).Add(Arg.Is<Message>(m => m.ProviderMessageId == "provider-3"));
    }
}
