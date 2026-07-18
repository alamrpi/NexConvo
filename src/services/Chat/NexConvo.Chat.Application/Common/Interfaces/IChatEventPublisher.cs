namespace NexConvo.Chat.Application.Common.Interfaces;

/// <summary>
/// Channel-neutral real-time fan-out seam for the Conversations CQRS slice (add-chat-inbox).
/// Chat's Api layer implements this with a SignalR adapter over the existing ChatHub envelope
/// (conv:{conversationId} and tenant:{tenantId}:agents groups) — the exact same pattern
/// NexConvo.Chat.Application.Rag.IReplyStreamSink already uses for the RAG pipeline's
/// token/complete/handoff events. This is a separate interface (not an extension of
/// IReplyStreamSink) because its events are agent-command-driven, not streaming-reply-driven,
/// and the two seams are consumed from different call sites (command handlers vs.
/// ReplyOrchestrator).
///
/// Delivery is a UX concern, not the source of truth: the persisted Conversation/Message rows
/// stay authoritative. Implementations MUST therefore be best-effort and never throw — a
/// real-time delivery failure must never fault the command handler's persistence path.
/// </summary>
public interface IChatEventPublisher
{
    /// <summary>A plain (non-streamed) message was appended to a conversation — e.g. an agent
    /// reply. Broadcast to the conversation's own viewers and the tenant's agent dashboard.</summary>
    Task PublishMessageAsync(Guid tenantId, ChatMessageEvent message, CancellationToken cancellationToken);

    /// <summary>A conversation was taken over by an agent (PendingHuman -> HumanHandling).</summary>
    Task PublishAssignedAsync(Guid tenantId, Guid conversationId, Guid agentUserId, DateTimeOffset assignedAt, CancellationToken cancellationToken);

    /// <summary>A conversation was marked resolved.</summary>
    Task PublishResolvedAsync(Guid tenantId, Guid conversationId, DateTimeOffset resolvedAt, CancellationToken cancellationToken);

    /// <summary>A resolved/closed conversation was reopened (back to AiHandling).</summary>
    Task PublishReopenedAsync(Guid tenantId, Guid conversationId, DateTimeOffset reopenedAt, CancellationToken cancellationToken);
}

/// <summary>
/// Transport-neutral shape of a persisted message, for the <see cref="IChatEventPublisher"/>
/// seam. Field-for-field mirror of
/// NexConvo.Chat.Application.Features.Conversations.Dtos.MessageDto so the Api-layer adapter can
/// forward an already-built MessageDto without remapping, while keeping this interface free of a
/// dependency on that Features namespace.
/// </summary>
public sealed record ChatMessageEvent(
    Guid ConversationId,
    Guid MessageId,
    string SenderRole,
    string? SenderName,
    string Body,
    DateTimeOffset SentAt,
    string? DeliveryStatus,
    double? Confidence);
