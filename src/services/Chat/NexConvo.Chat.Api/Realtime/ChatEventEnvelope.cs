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
