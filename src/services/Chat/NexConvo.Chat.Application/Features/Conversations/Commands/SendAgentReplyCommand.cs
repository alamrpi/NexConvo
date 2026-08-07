using MediatR;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Chat.Application.Features.Conversations.Dtos;

namespace NexConvo.Chat.Application.Features.Conversations.Commands;

/// <summary>Sends an agent's reply on a conversation. AgentUserId is the actor (from JWT), not user input.</summary>
public sealed record SendAgentReplyCommand(
    Guid ConversationId,
    Guid AgentUserId,
    string Text) : IRequest<Result<MessageDto>>;
