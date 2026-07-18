namespace NexConvo.Chat.Api.Realtime;

/// <summary>
/// Typed hub-to-client envelope, sent via the single "chatEvent" client method. The string
/// discriminator is the extension point: MCP (P3) adds "tool_executing" / "action_complete" as
/// new type constants + payload records — no new client methods, no protocol rewrite. SignalR's
/// default JSON protocol serializes this camelCase: {"type":"token","payload":{...}}.
/// </summary>
public sealed record ChatEventEnvelope(string Type, object Payload);

public static class ChatEventTypes
{
    public const string Token = "token";
    public const string Complete = "complete";
    public const string Handoff = "handoff";

    // Agent-facing inbox events (add-chat-inbox): fired after a Conversations CQRS command
    // successfully persists, alongside the RAG pipeline's existing Token/Complete/Handoff events
    // on the same envelope/group scheme — no new hub, no new groups.
    public const string Message = "message";
    public const string Assigned = "assigned";
    public const string Resolved = "resolved";
    public const string Reopened = "reopened";
}

public sealed record TokenEventPayload(Guid ConversationId, string Text);

/// <summary>A grounding citation keyed to the [n] markers in the reply text. No chunk content —
/// knowledge text must not ship to widget clients; ids let the agent console deep-link.</summary>
public sealed record CitationDto(int Index, string ChunkId, string DocumentId, double Score);

public sealed record ReplyCompleteEventPayload(
    Guid ConversationId,
    Guid MessageId,
    string Text,
    double ConfidenceScore,
    string ConfidenceBand,
    IReadOnlyList<CitationDto> Citations);

public sealed record HandoffEventPayload(
    Guid ConversationId,
    Guid EscalationId,
    string Reason,
    DateTimeOffset RaisedAt);

/// <summary>
/// A plain agent (or otherwise non-streamed) message appended to a conversation — distinct from
/// the AI pipeline's Token/Complete pair, which streams incrementally before persistence. Message
/// is emitted once, after the message row is already committed, mirroring MessageDto's field
/// shape (Features/Conversations/Dtos/MessageDto.cs) so inbox clients can append it directly to
/// their message list without remapping.
/// </summary>
public sealed record MessageEventPayload(
    Guid ConversationId,
    Guid MessageId,
    string SenderRole,
    string? SenderName,
    string Body,
    DateTimeOffset SentAt,
    string? DeliveryStatus,
    double? Confidence);

/// <summary>Fired when a conversation transitions to HumanHandling via TakeOverConversationCommand.</summary>
public sealed record AssignedEventPayload(
    Guid ConversationId,
    Guid AgentUserId,
    DateTimeOffset AssignedAt);

/// <summary>Fired when a conversation transitions to Resolved via ResolveConversationCommand.</summary>
public sealed record ResolvedEventPayload(
    Guid ConversationId,
    DateTimeOffset ResolvedAt);

/// <summary>Fired when a conversation transitions back to AiHandling via ReopenConversationCommand.</summary>
public sealed record ReopenedEventPayload(
    Guid ConversationId,
    DateTimeOffset ReopenedAt);
