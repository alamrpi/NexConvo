using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Domain.Exceptions;

/// <summary>
/// Thrown when a Conversation aggregate is asked to transition to a <see cref="ConversationState"/>
/// that is not reachable from its current state (e.g. Closed -> AiHandling).
/// </summary>
public sealed class InvalidConversationStateTransitionException(ConversationState from, ConversationState to)
    : Exception($"Cannot transition conversation from '{from}' to '{to}'.")
{
    public ConversationState From { get; } = from;
    public ConversationState To { get; } = to;
}
