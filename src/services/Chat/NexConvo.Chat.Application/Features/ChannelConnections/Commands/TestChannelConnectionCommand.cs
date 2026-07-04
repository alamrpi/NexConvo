using MediatR;
using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Application.Features.ChannelConnections.Commands;

/// <summary>
/// Verifies a channel access token by making a lightweight API call.
/// For ChatChannel.Web no external call is made — always succeeds.
/// </summary>
public sealed record TestChannelConnectionCommand(
    ChatChannel Channel,
    string AccessToken) : IRequest<TestChannelConnectionResult>;

public sealed record TestChannelConnectionResult(bool Success, string? AccountName, string? ErrorMessage);
