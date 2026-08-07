using MediatR;
using NexConvo.BuildingBlocks.Results;

namespace NexConvo.Chat.Application.Features.Conversations.Commands;

/// <summary>Takes ownership of a conversation pending human handoff. AgentUserId is the actor (from JWT).</summary>
public sealed record TakeOverConversationCommand(
    Guid ConversationId,
    Guid AgentUserId) : IRequest<Result>;
