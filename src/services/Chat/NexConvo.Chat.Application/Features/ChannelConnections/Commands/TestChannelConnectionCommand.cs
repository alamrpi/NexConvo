using MediatR;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Application.Features.ChannelConnections.Commands;

/// <summary>
/// Live "Test Connection" probe (Standard 22: test-then-save) for a channel access token supplied
/// by the user before saving. Does not persist — see <see cref="TestChannelConnectionByIdCommand"/>
/// for re-testing an already-saved connection.
/// For ChatChannel.Web no external call is made — always succeeds.
/// </summary>
public sealed record TestChannelConnectionCommand(
    ChatChannel Channel,
    string AccessToken,
    string? ExternalAccountId) : IRequest<ConnectionHealth>;
