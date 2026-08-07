namespace NexConvo.Chat.Domain.Enums;

public enum EscalationReason
{
    LowConfidence,
    TriggerPhrase,
    SentimentNegative,
    MaxUnansweredExceeded,
    ExplicitAgentRequest,
    NoAiConfig,
}
