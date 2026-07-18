using NexConvo.Chat.Domain.Entities;
using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Application.Features.Conversations.Dtos;

/// <summary>
/// Read model for a single message, shaped to match the frontend's mock <c>Message</c> type
/// (frontend/src/features/chat/mock-data.ts) field-for-field so the existing
/// <c>shared/ui/chat/*</c> components need zero prop changes.
/// </summary>
public sealed record MessageDto(
    Guid Id,
    Guid ConversationId,
    string SenderRole,
    string? SenderName,
    string Body,
    DateTimeOffset SentAt,
    string? DeliveryStatus,
    double? Confidence)
{
    public static MessageDto FromEntity(Message message) => new(
        message.Id,
        message.ConversationId,
        ToSenderRole(message.Sender.Role),
        message.Sender.DisplayRef,
        message.Body,
        message.CreatedAt,
        message.Direction == MessageDirection.Outbound ? ToDeliveryStatus(message.DeliveryStatus) : null,
        message.Confidence);

    private static string ToSenderRole(MessageSenderRole role) => role switch
    {
        MessageSenderRole.Contact => "Contact",
        MessageSenderRole.Ai => "Ai",
        MessageSenderRole.Agent => "Agent",
        // System/ToolCall/ToolResult: the frontend SenderRole union only knows System beyond
        // Contact/Ai/Agent today (ToolCall/ToolResult are reserved for a future MCP agentic loop,
        // per MessageSenderRole's own doc comment) — collapse them to System rather than throwing.
        _ => "System",
    };

    private static string ToDeliveryStatus(MessageDeliveryStatus status) => status switch
    {
        MessageDeliveryStatus.Sent => "Sent",
        MessageDeliveryStatus.Delivered => "Delivered",
        MessageDeliveryStatus.Failed => "Failed",
        // Pending has no distinct frontend rendering yet (DeliveryStatusIcon has no "pending"
        // branch) — Sent is the closest visual (single checkmark) until a dedicated state exists.
        _ => "Sent",
    };
}
