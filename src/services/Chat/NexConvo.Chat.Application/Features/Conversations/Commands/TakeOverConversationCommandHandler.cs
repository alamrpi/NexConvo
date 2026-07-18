using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Chat.Application.Common.Interfaces;

namespace NexConvo.Chat.Application.Features.Conversations.Commands;

public sealed class TakeOverConversationCommandHandler(
    IChatDbContext db,
    ITenantContext tenant,
    IChatEventPublisher eventPublisher,
    ILogger<TakeOverConversationCommandHandler> logger)
    : IRequestHandler<TakeOverConversationCommand, Result>
{
    public async Task<Result> Handle(TakeOverConversationCommand cmd, CancellationToken ct)
    {
        var tenantId = tenant.TenantId;

        var conversation = await db.Conversations
            .FirstOrDefaultAsync(c => c.Id == cmd.ConversationId && c.TenantId == tenantId, ct);

        if (conversation is null)
        {
            // Deliberately generic — no conversation id in the message; see
            // GetConversationMessagesQueryHandler for the same fix and rationale (Result.Error
            // flows verbatim into the HTTP response body, so a cross-tenant caller must not learn
            // the id exists via this message).
            logger.LogInformation("Conversation {ConversationId} not found for tenant {TenantId}", cmd.ConversationId, tenantId);
            return Result.NotFound("Conversation not found.");
        }

        // Conversation.TakeOver throws InvalidConversationStateTransitionException if not
        // PendingHuman — let it propagate; the controller translates it to 409 Conflict.
        conversation.TakeOver(cmd.AgentUserId);

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Agent {AgentUserId} took over conversation {ConversationId}",
            cmd.AgentUserId, cmd.ConversationId);

        // Fire after successful persistence; IChatEventPublisher is best-effort and never throws.
        await eventPublisher.PublishAssignedAsync(
            tenantId, conversation.Id, cmd.AgentUserId, conversation.UpdatedAt, ct);

        return Result.Success();
    }
}
