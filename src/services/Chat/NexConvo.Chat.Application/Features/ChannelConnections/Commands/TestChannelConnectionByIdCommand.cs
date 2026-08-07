using MediatR;
using NexConvo.BuildingBlocks.Domain.Health;

namespace NexConvo.Chat.Application.Features.ChannelConnections.Commands;

/// <summary>
/// Re-tests an already-saved channel connection (Standard 22): loads the stored connection by
/// tenant + id, decrypts its access token, probes the provider, and persists the resulting
/// <see cref="ConnectionHealth"/> via <c>ChannelConnection.ApplyHealth</c> so the settings UI badge
/// reflects the latest test. This is the "re-test a saved connection" path used by the frontend
/// drawer + row — distinct from <see cref="TestChannelConnectionCommand"/>, which tests a token
/// before it is ever saved and does not persist.
/// </summary>
public sealed record TestChannelConnectionByIdCommand(Guid ConnectionId) : IRequest<ConnectionHealth>;
