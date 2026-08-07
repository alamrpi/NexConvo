using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Application.Features.Conversations.Dtos;

namespace NexConvo.Chat.Application.Features.Conversations.Queries;

public sealed class GetConversationMessagesQueryHandler(
    IChatDbContext db,
    ITenantContext tenant,
    ILogger<GetConversationMessagesQueryHandler> logger)
    : IRequestHandler<GetConversationMessagesQuery, Result<CursorPagedResult<MessageDto>>>
{
    public async Task<Result<CursorPagedResult<MessageDto>>> Handle(
        GetConversationMessagesQuery request,
        CancellationToken ct)
    {
        var tenantId = tenant.TenantId;

        var conversation = await db.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ConversationId && c.TenantId == tenantId, ct);

        // Same "not found" response whether the conversation doesn't exist, belongs to another
        // tenant, or the requester lacks access — never reveal existence to an unauthorized caller.
        var isAuthorized = conversation is not null &&
            (request.RequestingAgentHasReadPermission || conversation.AssignedAgentUserId == request.RequestingAgentUserId);

        if (!isAuthorized)
        {
            logger.LogInformation(
                "Conversation {ConversationId} not found or not accessible to agent {AgentUserId}",
                request.ConversationId, request.RequestingAgentUserId);

            // Deliberately generic — no conversation id in the message. This response must be
            // byte-for-byte identical whether the conversation doesn't exist, belongs to another
            // tenant, or the requester merely lacks access (spec: "without revealing whether the
            // conversation exists"), and Result.Error flows verbatim into the HTTP response body
            // via NotFoundObjectResult, so embedding the id here would leak it right back out.
            return Result<CursorPagedResult<MessageDto>>.NotFound("Conversation not found.");
        }

        var pageSize = request.PageSize is > 0 and <= 200 ? request.PageSize : 50;

        var query = db.Messages
            .Where(m => m.ConversationId == request.ConversationId)
            .AsNoTracking()
            .AsQueryable();

        if (ConversationCursor.TryDecode(request.Cursor, out var cursorCreatedAt, out _))
        {
            // Ascending/chronological paging: the cursor marks the last-seen message, so the next
            // page continues strictly after it.
            query = query.Where(m => m.CreatedAt > cursorCreatedAt);
        }

        var page = await query
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .Take(pageSize + 1)
            .ToListAsync(ct);

        var hasMore = page.Count > pageSize;
        var items = hasMore ? page.Take(pageSize).ToList() : page;

        var dtos = items.Select(MessageDto.FromEntity).ToList();

        var nextCursor = hasMore
            ? ConversationCursor.Encode(items[^1].CreatedAt, items[^1].Id)
            : null;

        return Result.Success(new CursorPagedResult<MessageDto>(dtos, nextCursor));
    }
}
