using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Domain.Exceptions;

/// <summary>
/// Thrown when the RAG orchestrator determines an AI-generated reply must not be sent to the
/// contact (e.g. low confidence, trigger phrase) and the conversation should escalate instead.
/// </summary>
public sealed class AiReplySuppressedException(EscalationReason reason, string? detail)
    : Exception($"AI reply suppressed: {reason}. {detail}".Trim())
{
    public EscalationReason Reason { get; } = reason;
}
