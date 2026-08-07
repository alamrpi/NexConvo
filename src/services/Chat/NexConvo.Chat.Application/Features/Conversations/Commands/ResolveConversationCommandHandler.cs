using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Chat.Application.Common.Interfaces;

namespace NexConvo.Chat.Application.Features.Conversations.Commands;

public sealed class ResolveConversationCommandHandler(
    IChatDbContext db,
    ITenantContext tenant,
    IChatEventPublisher eventPublisher,
    ILogger<ResolveConversationCommandHandler> logger)
    : IRequestHandler<ResolveConversationCommand, Result>
{
    public async Task<Result> Handle(ResolveConversationCommand cmd, CancellationToken ct)
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

        // Conversation.Resolve throws InvalidConversationStateTransitionException if not
        // HumanHandling/AiHandling — let it propagate; the controller translates it to 409 Conflict.
        conversation.Resolve();

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Agent {ActorUserId} resolved conversation {ConversationId}",
            cmd.ActorUserId, cmd.ConversationId);

        // Fire after successful persistence; IChatEventPublisher is best-effort and never throws.
        await eventPublisher.PublishResolvedAsync(tenantId, conversation.Id, conversation.UpdatedAt, ct);

        return Result.Success();
    }
}
