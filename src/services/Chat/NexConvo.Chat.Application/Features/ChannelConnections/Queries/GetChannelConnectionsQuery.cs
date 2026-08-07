using MediatR;
using NexConvo.Chat.Application.Features.ChannelConnections.Dtos;

namespace NexConvo.Chat.Application.Features.ChannelConnections.Queries;

/// <summary>Returns all channel connections for the current tenant (tenant from JWT).</summary>
public sealed record GetChannelConnectionsQuery : IRequest<List<ChannelConnectionDto>>;
