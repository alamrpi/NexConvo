using MediatR;
using NexConvo.BuildingBlocks.Results;

namespace NexConvo.Chat.Application.Features.ChannelConnections.Commands;

/// <summary>Soft-deletes a channel connection (sets IsActive = false). Never hard-deletes.</summary>
public sealed record DeleteChannelConnectionCommand(
    Guid ConnectionId,
    Guid ActorUserId) : IRequest<Result>;
