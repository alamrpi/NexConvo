using MediatR;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Chat.Application.Features.Conversations.Dtos;

namespace NexConvo.Chat.Application.Features.Conversations.Queries;

/// <summary>
/// Fetches a conversation's message history in chronological order (oldest first), paged via an
/// opaque Cursor. Authorization (assigned agent OR conversations:read) is enforced in the handler
/// via <see cref="RequestingAgentUserId"/> + <see cref="RequestingAgentHasReadPermission"/> rather
/// than in the controller, so the same "not found" response is returned whether the conversation
/// truly doesn't exist or the requester just lacks access (spec: "without revealing whether the
/// conversation exists").
/// </summary>
public sealed record GetConversationMessagesQuery(
    Guid ConversationId,
    Guid RequestingAgentUserId,
    bool RequestingAgentHasReadPermission,
    string? Cursor,
    int PageSize = 50) : IRequest<Result<CursorPagedResult<MessageDto>>>;
