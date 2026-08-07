using MediatR;
using NexConvo.BuildingBlocks.Results;

namespace NexConvo.Chat.Application.Features.Conversations.Commands;

/// <summary>Marks an actively-handled conversation as resolved. ActorUserId is the actor (from JWT), for audit purposes.</summary>
public sealed record ResolveConversationCommand(
    Guid ConversationId,
    Guid ActorUserId) : IRequest<Result>;
