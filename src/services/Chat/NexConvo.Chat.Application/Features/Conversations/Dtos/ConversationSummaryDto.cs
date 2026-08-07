using NexConvo.Chat.Domain.Entities;

namespace NexConvo.Chat.Application.Features.Conversations.Dtos;

/// <summary>
/// Read model for one row in the conversation list, shaped to match the frontend's mock
/// <c>Conversation</c> type (frontend/src/features/chat/mock-data.ts) minus the embedded
/// <c>messages</c> array (fetched separately via <c>GetConversationMessagesQuery</c>).
///
/// Known gaps vs. the mock shape, tracked as follow-up rather than blocking this slice
/// (no schema for these exists yet on <see cref="Conversation"/> — see design.md's
/// "No database schema changes expected" note):
/// - <see cref="AssignedAgentName"/> is always null: Chat's DB only has <c>AssignedAgentUserId</c>,
///   and the display name lives in the Identity service with no lookup wired here.
/// - <see cref="UnreadCount"/> is always 0: no read-tracking column exists yet.
/// - <see cref="SlaExpiresAt"/> is always null and <see cref="Tags"/> always empty: no SLA/tag
///   columns exist yet.
/// - <see cref="ContactName"/> is always null: Contact records are owned by CoreCrm, not joined here.
/// </summary>
public sealed record ConversationSummaryDto(
    Guid Id,
    string State,
    string Channel,
    string? ContactName,
    string ContactHandle,
    string LastMessagePreview,
    DateTimeOffset LastMessageAt,
    bool LastMessageFromAi,
    int UnreadCount,
    string? AssignedAgentName,
    DateTimeOffset? SlaExpiresAt,
    IReadOnlyList<string> Tags)
{
    public static ConversationSummaryDto FromEntity(
        Conversation conversation, Message? lastMessage) => new(
        conversation.Id,
        conversation.State.ToString(),
        conversation.Channel.Channel.ToFrontendChannel(),
        ContactName: null,
        ContactHandle: conversation.Channel.ExternalConversationId,
        LastMessagePreview: lastMessage?.Body ?? string.Empty,
        LastMessageAt: lastMessage?.CreatedAt ?? conversation.UpdatedAt,
        LastMessageFromAi: lastMessage?.Sender.Role == Domain.Enums.MessageSenderRole.Ai,
        UnreadCount: 0,
        AssignedAgentName: null,
        SlaExpiresAt: null,
        Tags: []);
}
