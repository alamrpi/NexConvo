using MediatR;
using NexConvo.BuildingBlocks.Results;

namespace NexConvo.Chat.Application.Features.Conversations.Commands;

/// <summary>Reopens a resolved/closed conversation back to AI handling. ActorUserId is the actor (from JWT), for audit purposes.</summary>
public sealed record ReopenConversationCommand(
    Guid ConversationId,
    Guid ActorUserId) : IRequest<Result>;
