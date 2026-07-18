using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Application.Features.Conversations.Dtos;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Chat.Domain.Exceptions;

namespace NexConvo.Chat.Application.Features.Conversations.Commands;

public sealed class SendAgentReplyCommandHandler(
    IChatDbContext db,
    ITenantContext tenant,
    IChatEventPublisher eventPublisher,
    ILogger<SendAgentReplyCommandHandler> logger)
    : IRequestHandler<SendAgentReplyCommand, Result<MessageDto>>
{
    public async Task<Result<MessageDto>> Handle(SendAgentReplyCommand cmd, CancellationToken ct)
    {
        var tenantId = tenant.TenantId;

        var conversation = await db.Conversations
            .FirstOrDefaultAsync(c => c.Id == cmd.ConversationId && c.TenantId == tenantId, ct);

        if (conversation is null)
        {
            // Deliberately generic — no conversation id in the message, mirroring
            // GetConversationMessagesQueryHandler's fix for the same leak: Result.Error flows
            // verbatim into the HTTP response body via NotFoundObjectResult, so a cross-tenant
            // caller must not learn the id exists via this message.
            logger.LogInformation("Conversation {ConversationId} not found for tenant {TenantId}", cmd.ConversationId, tenantId);
            return Result<MessageDto>.NotFound("Conversation not found.");
        }

        // Conversation.AppendAgentReply has no state guard of its own (it's a plain append, unlike
        // TakeOver/Resolve/Reopen) — per spec "Reply to a resolved conversation is rejected", the
        // terminal states (Resolved/Closed) must not accept new outbound agent messages. Guarding
        // here rather than adding a check to the domain method itself, since Conversation.cs is a
        // shared, already-stable aggregate this slice only calls into, not reimplements.
        if (conversation.State is ConversationState.Resolved or ConversationState.Closed)
        {
            throw new InvalidConversationStateTransitionException(conversation.State, conversation.State);
        }

        var message = conversation.AppendAgentReply(cmd.AgentUserId, cmd.Text);
        db.Messages.Add(message);

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Agent {AgentUserId} sent reply on conversation {ConversationId}",
            cmd.AgentUserId, cmd.ConversationId);

        var dto = MessageDto.FromEntity(message);

        // Fire after successful persistence; IChatEventPublisher is best-effort and never throws.
        await eventPublisher.PublishMessageAsync(
            tenantId,
            new ChatMessageEvent(
                dto.ConversationId, dto.Id, dto.SenderRole, dto.SenderName, dto.Body, dto.SentAt,
                dto.DeliveryStatus, dto.Confidence),
            ct);

        return Result.Success(dto);
    }
}
