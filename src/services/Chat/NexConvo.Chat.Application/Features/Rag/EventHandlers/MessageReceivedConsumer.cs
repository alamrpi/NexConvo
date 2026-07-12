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
///
/// Tenant scoping: a MassTransit consumer has no ambient HTTP request, so the DI-scoped
/// IChatDbContext (backed by HttpTenantContext) would open a tenant-less connection and RLS would
/// silently hide/reject every row. This consumer instead builds its own context via
/// IChatDbContextFactory, pinned to the event's own TenantId — mirrors Knowledge's
/// KnowledgeIngestionJob pattern for the same class of problem in Hangfire jobs.
///
/// Concurrent-first-message race: two near-simultaneous inbound messages on a brand-new channel
/// thread can both fail to find an existing Conversation and both attempt to INSERT one — the
/// database's unique partial index (idx_conversations_unique_open_thread, Task 6) is the real
/// backstop and rejects the loser's insert with a DbUpdateException. Rather than letting that
/// fault the whole message (MassTransit would retry it as a brand-new attempt, which is safe but
/// noisy and slow), the loser re-queries for the winner's row and appends to it instead.
/// </summary>
public sealed class MessageReceivedConsumer(
    IChatDbContextFactory dbFactory,
    ISender sender,
    ILogger<MessageReceivedConsumer> logger) : IConsumer<MessageReceivedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<MessageReceivedIntegrationEvent> context)
    {
        var message = context.Message;
        await using var db = dbFactory.CreateForTenant(message.TenantId);

        var conversation = await FindConversationAsync(db, message, context.CancellationToken);
        var conversationWasNew = conversation is null;

        conversation ??= StartConversation(db, message);

        var inbound = conversation.AppendInbound(message.ProviderMessageId, message.Body);
        db.Messages.Add(inbound);

        try
        {
            await db.SaveChangesAsync(context.CancellationToken);
        }
        catch (DbUpdateException) when (conversationWasNew)
        {
            // Lost the race to create this conversation — another delivery (of this same message,
            // or a different one on the same channel thread) won. Re-query for the winner's row
            // and append the inbound message to it instead of faulting the whole delivery.
            logger.LogInformation(
                "Conversation creation raced for tenant {TenantId}, channel thread {ExternalSenderId}; retrying against the existing row",
                message.TenantId, message.ExternalSenderId);

            await using var retryDb = dbFactory.CreateForTenant(message.TenantId);
            conversation = await FindConversationAsync(retryDb, message, context.CancellationToken)
                ?? throw new InvalidOperationException(
                    $"Conversation creation raced for tenant {message.TenantId} but no row was found on retry.");

            var retryInbound = conversation.AppendInbound(message.ProviderMessageId, message.Body);
            retryDb.Messages.Add(retryInbound);
            await retryDb.SaveChangesAsync(context.CancellationToken);
        }

        logger.LogInformation(
            "Inbound message processed for conversation {ConversationId}, dispatching RAG reply",
            conversation.Id);

        await sender.Send(new GenerateRagReplyCommand(message.TenantId, conversation.Id), context.CancellationToken);
    }

    private static Task<Conversation?> FindConversationAsync(
        IChatDbContext db, MessageReceivedIntegrationEvent message, CancellationToken cancellationToken) =>
        db.Conversations
            .FirstOrDefaultAsync(
                c => c.TenantId == message.TenantId
                    && c.Channel.Channel == message.Channel
                    && c.Channel.ExternalConversationId == message.ExternalSenderId,
                cancellationToken);

    private static Conversation StartConversation(IChatDbContext db, MessageReceivedIntegrationEvent message)
    {
        var channelIdentity = new ChannelIdentity(message.Channel, message.ExternalSenderId);
        var conversation = Conversation.StartAiHandling(message.TenantId, channelIdentity, contactId: null);
        db.Conversations.Add(conversation);
        return conversation;
    }
}
