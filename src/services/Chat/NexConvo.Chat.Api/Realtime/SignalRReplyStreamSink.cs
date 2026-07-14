using Microsoft.AspNetCore.SignalR;
using NexConvo.Chat.Application.Rag;

namespace NexConvo.Chat.Api.Realtime;

/// <summary>
/// SignalR adapter for the channel-neutral IReplyStreamSink seam. Fans each event to the hub
/// groups (the Redis backplane relays them to clients on other pods). Best-effort by contract:
/// every send is caught and logged — a real-time delivery failure must never fault the
/// authoritative generation/persistence path in ReplyOrchestrator.
/// </summary>
public sealed class SignalRReplyStreamSink(
    IHubContext<ChatHub> hub,
    ILogger<SignalRReplyStreamSink> logger) : IReplyStreamSink
{
    public async Task OnTokenAsync(Guid tenantId, Guid conversationId, string token, CancellationToken cancellationToken)
    {
        try
        {
            // One send per chunk, unbuffered — live viewers see the reply grow token by token.
            await hub.Clients.Group(ChatHub.ConversationGroup(conversationId)).SendAsync(
                ChatHub.ClientMethod,
                new ChatEventEnvelope(ChatEventTypes.Token, new TokenEventPayload(conversationId, token)),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to stream token to conversation group {ConversationId}", conversationId);
        }
    }

    public async Task OnCompletedAsync(Guid tenantId, Guid conversationId, ReplyCompletedNotification completed, CancellationToken cancellationToken)
    {
        try
        {
            await hub.Clients.Group(ChatHub.ConversationGroup(conversationId)).SendAsync(
                ChatHub.ClientMethod,
                new ChatEventEnvelope(ChatEventTypes.Complete, new ReplyCompleteEventPayload(
                    conversationId,
                    completed.MessageId,
                    completed.Text,
                    completed.Confidence.Score,
                    completed.Confidence.Band.ToString(),
                    completed.Citations.Select(c => new CitationDto(c.Index, c.ChunkId, c.DocumentId, c.Score)).ToList())),
                cancellationToken);

            logger.LogInformation("Streamed reply to {ConversationId}", conversationId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send reply-complete event to conversation group {ConversationId}", conversationId);
        }
    }

    public async Task OnHandoffAsync(Guid tenantId, Guid conversationId, HandoffNotification handoff, CancellationToken cancellationToken)
    {
        try
        {
            var envelope = new ChatEventEnvelope(ChatEventTypes.Handoff, new HandoffEventPayload(
                conversationId, handoff.EscalationId, handoff.Reason, handoff.RaisedAt));

            // Agents group: dashboards light up with the waiting conversation. Conversation group:
            // viewers that just watched tokens stream learn the draft was suppressed and discard it.
            await hub.Clients
                .Groups(ChatHub.AgentsGroup(tenantId), ChatHub.ConversationGroup(conversationId))
                .SendAsync(ChatHub.ClientMethod, envelope, cancellationToken);

            logger.LogInformation(
                "Broadcast handoff for conversation {ConversationId} to tenant {TenantId} agents", conversationId, tenantId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast handoff for conversation {ConversationId}", conversationId);
        }
    }
}
