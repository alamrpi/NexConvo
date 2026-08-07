using NexConvo.Chat.Domain.Enums;
using NexConvo.Chat.Domain.ValueObjects;

namespace NexConvo.Chat.Domain.Entities;

/// <summary>
/// A single inbound or outbound message within a <see cref="Conversation"/>.
/// Created only via <see cref="Conversation"/>'s factory methods — never directly.
/// </summary>
public class Message
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    public Guid ConversationId { get; private set; }
    public MessageSender Sender { get; private set; } = null!;
    public MessageDirection Direction { get; private set; }
    public string Body { get; private set; } = null!;
    public string? ProviderMessageId { get; private set; }
    public MessageDeliveryStatus DeliveryStatus { get; private set; }
    public double? Confidence { get; private set; }

    /// <summary>Nullable JSONB payload reserved for future ToolCall/ToolResult structured data — unused today.</summary>
    public string? StructuredPayload { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private Message()
    {
        // EF Core
    }

    internal static Message Inbound(Guid tenantId, Guid conversationId, MessageSender sender, string? providerMessageId, string body) =>
        new()
        {
            TenantId = tenantId,
            ConversationId = conversationId,
            Sender = sender,
            Direction = MessageDirection.Inbound,
            Body = body,
            ProviderMessageId = providerMessageId,
            DeliveryStatus = MessageDeliveryStatus.Delivered,
        };

    internal static Message Outbound(Guid tenantId, Guid conversationId, MessageSender sender, string body, double? confidence) =>
        new()
        {
            TenantId = tenantId,
            ConversationId = conversationId,
            Sender = sender,
            Direction = MessageDirection.Outbound,
            Body = body,
            DeliveryStatus = MessageDeliveryStatus.Sent,
            Confidence = confidence,
        };
}
