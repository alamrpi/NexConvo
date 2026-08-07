using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Domain.ValueObjects;

public sealed record MessageSender(MessageSenderRole Role, string? DisplayRef, Guid? UserId)
{
    public static MessageSender Contact(string? displayRef) => new(MessageSenderRole.Contact, displayRef, null);
    public static MessageSender Ai() => new(MessageSenderRole.Ai, "AI Assistant", null);
    public static MessageSender Agent(Guid userId, string displayRef) => new(MessageSenderRole.Agent, displayRef, userId);
}
