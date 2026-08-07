namespace NexConvo.Chat.Application.Features.Conversations.Dtos;

/// <summary>
/// A cursor-paged page of results. <see cref="NextCursor"/> is opaque to the caller — pass it
/// back as the next request's Cursor to get the following page; null means there is no more data.
/// Per design.md decision 1 (GetConversationsQuery/GetConversationMessagesQuery use Cursor, not
/// page-number paging), unlike the offset-based PagedResult&lt;T&gt; used elsewhere (e.g. Identity).
/// </summary>
public sealed record CursorPagedResult<T>(IReadOnlyList<T> Items, string? NextCursor);
