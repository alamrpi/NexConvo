using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Application.Features.Conversations.Dtos;
using NexConvo.Chat.Domain.Entities;

namespace NexConvo.Chat.Application.Features.Conversations.Queries;

public sealed class GetConversationsQueryHandler(
    IChatDbContext db,
    ITenantContext tenant,
    ILogger<GetConversationsQueryHandler> logger)
    : IRequestHandler<GetConversationsQuery, CursorPagedResult<ConversationSummaryDto>>
{
    public async Task<CursorPagedResult<ConversationSummaryDto>> Handle(
        GetConversationsQuery request,
        CancellationToken ct)
    {
        var tenantId = tenant.TenantId;
        var pageSize = request.PageSize is > 0 and <= 100 ? request.PageSize : 25;

        var query = db.Conversations
            .Where(c => c.TenantId == tenantId)
            .AsNoTracking()
            .AsQueryable();

        if (request.StateFilter is not null)
        {
            query = query.Where(c => c.State == request.StateFilter);
        }

        var channelFilter = ConversationChannelMapper.FromFrontendChannel(request.ChannelFilter);
        if (channelFilter is not null)
        {
            query = query.Where(c => c.Channel.Channel == channelFilter);
        }

        if (request.AssignedToMe)
        {
            query = query.Where(c => c.AssignedAgentUserId == request.RequestingAgentUserId);
        }

        // Cursor is (UpdatedAt, Id) but only UpdatedAt is used as the WHERE boundary — Guid has no
        // SQL-translatable ordering via EF/Npgsql, so exact-tick ties (vanishingly rare for chat
        // activity timestamps) may repeat/skip a single row across pages rather than risk a
        // client-evaluation fallback or a runtime translation exception. Id is still used as the
        // in-memory ORDER BY tie-break (below) and as part of the encoded cursor for that reason.
        if (ConversationCursor.TryDecode(request.Cursor, out var cursorUpdatedAt, out _))
        {
            query = query.Where(c => c.UpdatedAt < cursorUpdatedAt);
        }

        var page = await query
            .OrderByDescending(c => c.UpdatedAt)
            .ThenByDescending(c => c.Id)
            .Take(pageSize + 1)
            .ToListAsync(ct);

        var hasMore = page.Count > pageSize;
        var items = hasMore ? page.Take(pageSize).ToList() : page;

        var conversationIds = items.Select(c => c.Id).ToList();
        var lastMessages = await GetLastMessagesAsync(conversationIds, ct);

        var dtos = items
            .Select(c => ConversationSummaryDto.FromEntity(c, lastMessages.GetValueOrDefault(c.Id)))
            .ToList();

        var nextCursor = hasMore
            ? ConversationCursor.Encode(items[^1].UpdatedAt, items[^1].Id)
            : null;

        logger.LogInformation(
            "Retrieved {Count} conversations for tenant {TenantId} (hasMore={HasMore})",
            dtos.Count, tenantId, hasMore);

        return new CursorPagedResult<ConversationSummaryDto>(dtos, nextCursor);
    }

    /// <summary>
    /// One most-recent message per conversation id, fetched in a single grouped query rather than
    /// N+1'ing per conversation.
    /// </summary>
    private async Task<Dictionary<Guid, Message>> GetLastMessagesAsync(
        List<Guid> conversationIds, CancellationToken ct)
    {
        if (conversationIds.Count == 0)
        {
            return [];
        }

        var latestPerConversation = await db.Messages
            .Where(m => conversationIds.Contains(m.ConversationId))
            .GroupBy(m => m.ConversationId)
            .Select(g => g.OrderByDescending(m => m.CreatedAt).First())
            .AsNoTracking()
            .ToListAsync(ct);

        return latestPerConversation.ToDictionary(m => m.ConversationId);
    }
}
