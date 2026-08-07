using NexConvo.Chat.Domain.Enums;
using NexConvo.Chat.Domain.ValueObjects;

namespace NexConvo.Chat.Application.Rag;

public abstract record ReplyOutcome;

public sealed record AnsweredOutcome(Guid MessageId, string Text, RagConfidence Confidence) : ReplyOutcome;

public sealed record HandoffOutcome(Guid EscalationId, EscalationReason Reason) : ReplyOutcome;
