using NexConvo.Chat.Domain.ValueObjects;

namespace NexConvo.Chat.Application.Rag;

/// <summary>
/// Channel-neutral delivery seam for the RAG reply pipeline. Chat's Api layer implements it with
/// a SignalR adapter; the future Voice service will implement it with Cartesia TTS — the
/// orchestrator never learns which transport is behind it.
///
/// Delivery is a UX concern, not the source of truth: the persisted Message/Escalation rows stay
/// authoritative. Implementations MUST therefore be best-effort and never throw — a delivery
/// failure is logged by the adapter, never propagated into the generation/persistence path.
/// </summary>
public interface IReplyStreamSink
{
    /// <summary>One LLM token/chunk, forwarded as it arrives — implementations must not buffer.</summary>
    Task OnTokenAsync(Guid tenantId, Guid conversationId, string token, CancellationToken cancellationToken);

    /// <summary>The reply was persisted; carries the authoritative message id, citations, and confidence.</summary>
    Task OnCompletedAsync(Guid tenantId, Guid conversationId, ReplyCompletedNotification completed, CancellationToken cancellationToken);

    /// <summary>The conversation transitioned to PendingHuman; fired only after the escalation is persisted.</summary>
    Task OnHandoffAsync(Guid tenantId, Guid conversationId, HandoffNotification handoff, CancellationToken cancellationToken);
}

/// <summary>
/// A retrieved chunk that grounded the reply, keyed to the [n] citation markers in the text.
/// Deliberately excludes chunk content — knowledge text must not ship to widget clients;
/// ChunkId/DocumentId let the agent console deep-link into the knowledge base instead.
/// </summary>
public sealed record ReplyCitation(int Index, string ChunkId, string DocumentId, double Score);

public sealed record ReplyCompletedNotification(
    Guid MessageId,
    string Text,
    RagConfidence Confidence,
    IReadOnlyList<ReplyCitation> Citations);

public sealed record HandoffNotification(Guid EscalationId, string Reason, DateTimeOffset RaisedAt);
