using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Application.Features.Rag.Commands;
using NexConvo.Chat.Domain.Entities;
using NexConvo.Chat.Domain.ValueObjects;
using NexConvo.Contracts.Events.Chat;

namespace NexConvo.Chat.Application.Features.Rag.EventHandlers;

/// <summary>
/// Persists the inbound message and dispatches the RAG reply command. Idempotent via MassTransit's
/// EF inbox (dedupe by transport MessageId — Standard 18), configured on ChatDbContext.
/// </summary>
public sealed class MessageReceivedConsumer(
    IChatDbContext db,
    ISender sender,
    ILogger<MessageReceivedConsumer> logger) : IConsumer<MessageReceivedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<MessageReceivedIntegrationEvent> context)
    {
        var message = context.Message;

        var conversation = await db.Conversations
            .FirstOrDefaultAsync(
                c => c.TenantId == message.TenantId
                    && c.Channel.Channel == message.Channel
                    && c.Channel.ExternalConversationId == message.ExternalSenderId,
                context.CancellationToken);

        if (conversation is null)
        {
            var channelIdentity = new ChannelIdentity(message.Channel, message.ExternalSenderId);
            conversation = Conversation.StartAiHandling(message.TenantId, channelIdentity, contactId: null);
            db.Conversations.Add(conversation);
        }

        conversation.AppendInbound(message.ProviderMessageId, message.Body);

        await db.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation(
            "Inbound message processed for conversation {ConversationId}, dispatching RAG reply",
            conversation.Id);

        await sender.Send(new GenerateRagReplyCommand(conversation.Id), context.CancellationToken);
    }
}
