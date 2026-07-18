using Microsoft.AspNetCore.SignalR;
using NexConvo.Chat.Application.Common.Interfaces;

namespace NexConvo.Chat.Api.Realtime;

/// <summary>
/// SignalR adapter for the channel-neutral IChatEventPublisher seam (add-chat-inbox). Fans each
/// agent-command event to the same two ChatHub group families the RAG pipeline's
/// SignalRReplyStreamSink already uses — conv:{conversationId} and tenant:{tenantId}:agents — over
/// the shared "chatEvent" envelope. Best-effort by contract: every send is caught and logged, so a
/// real-time delivery failure never faults the calling command handler's persistence path.
/// </summary>
public sealed class SignalRChatEventPublisher(
    IHubContext<ChatHub> hub,
    ILogger<SignalRChatEventPublisher> logger) : IChatEventPublisher
{
    public async Task PublishMessageAsync(Guid tenantId, ChatMessageEvent message, CancellationToken cancellationToken)
    {
        try
        {
            var envelope = new ChatEventEnvelope(ChatEventTypes.Message, new MessageEventPayload(
                message.ConversationId,
                message.MessageId,
                message.SenderRole,
                message.SenderName,
                message.Body,
                message.SentAt,
                message.DeliveryStatus,
                message.Confidence));

            // Conversation group: an open thread appends the message live. Agents group: the
            // conversation list re-sorts/updates its preview + unread count without a refetch.
            await hub.Clients
                .Groups(ChatHub.ConversationGroup(message.ConversationId), ChatHub.AgentsGroup(tenantId))
                .SendAsync(ChatHub.ClientMethod, envelope, cancellationToken);

            logger.LogInformation(
                "Broadcast message event for conversation {ConversationId} to tenant {TenantId} agents",
                message.ConversationId, tenantId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast message event for conversation {ConversationId}", message.ConversationId);
        }
    }

    public async Task PublishAssignedAsync(Guid tenantId, Guid conversationId, Guid agentUserId, DateTimeOffset assignedAt, CancellationToken cancellationToken)
    {
        try
        {
            var envelope = new ChatEventEnvelope(ChatEventTypes.Assigned, new AssignedEventPayload(conversationId, agentUserId, assignedAt));

            await hub.Clients
                .Groups(ChatHub.ConversationGroup(conversationId), ChatHub.AgentsGroup(tenantId))
                .SendAsync(ChatHub.ClientMethod, envelope, cancellationToken);

            logger.LogInformation(
                "Broadcast assigned event for conversation {ConversationId} to tenant {TenantId} agents",
                conversationId, tenantId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast assigned event for conversation {ConversationId}", conversationId);
        }
    }

    public async Task PublishResolvedAsync(Guid tenantId, Guid conversationId, DateTimeOffset resolvedAt, CancellationToken cancellationToken)
    {
        try
        {
            var envelope = new ChatEventEnvelope(ChatEventTypes.Resolved, new ResolvedEventPayload(conversationId, resolvedAt));

            await hub.Clients
                .Groups(ChatHub.ConversationGroup(conversationId), ChatHub.AgentsGroup(tenantId))
                .SendAsync(ChatHub.ClientMethod, envelope, cancellationToken);

            logger.LogInformation(
                "Broadcast resolved event for conversation {ConversationId} to tenant {TenantId} agents",
                conversationId, tenantId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast resolved event for conversation {ConversationId}", conversationId);
        }
    }

    public async Task PublishReopenedAsync(Guid tenantId, Guid conversationId, DateTimeOffset reopenedAt, CancellationToken cancellationToken)
    {
        try
        {
            var envelope = new ChatEventEnvelope(ChatEventTypes.Reopened, new ReopenedEventPayload(conversationId, reopenedAt));

            await hub.Clients
                .Groups(ChatHub.ConversationGroup(conversationId), ChatHub.AgentsGroup(tenantId))
                .SendAsync(ChatHub.ClientMethod, envelope, cancellationToken);

            logger.LogInformation(
                "Broadcast reopened event for conversation {ConversationId} to tenant {TenantId} agents",
                conversationId, tenantId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast reopened event for conversation {ConversationId}", conversationId);
        }
    }
}
