using MediatR;
using NexConvo.Chat.Application.Features.Conversations.Dtos;
using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Application.Features.Conversations.Queries;

/// <summary>
/// Lists conversations for the current tenant (tenant from JWT via ITenantContext), most-recent-
/// activity first, optionally filtered by state/channel and/or restricted to the requesting
/// agent's own assigned conversations. Cursor is opaque — pass back CursorPagedResult.NextCursor
/// to fetch the following page.
/// </summary>
public sealed record GetConversationsQuery(
    ConversationState? StateFilter,
    string? ChannelFilter,
    bool AssignedToMe,
    Guid RequestingAgentUserId,
    string? Cursor,
    int PageSize = 25) : IRequest<CursorPagedResult<ConversationSummaryDto>>;
