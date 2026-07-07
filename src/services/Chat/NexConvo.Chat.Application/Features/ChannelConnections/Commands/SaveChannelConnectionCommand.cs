using MediatR;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Chat.Application.Features.ChannelConnections.Dtos;
using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Application.Features.ChannelConnections.Commands;

/// <summary>
/// Upserts a channel connection for the current tenant.
/// Tenant is resolved from ITenantContext (JWT) — never from the client (Standard 6).
/// Access token is encrypted at rest before persisting (Standard 15).
/// A new/changed access token is re-tested server-side before anything is persisted
/// (Standard 22) — see <see cref="SaveChannelConnectionCommandHandler"/>.
/// </summary>
public sealed record SaveChannelConnectionCommand(
    ChatChannel Channel,
    string ExternalAccountId,
    string? AccountName,
    string AccessToken,
    string? AppSecret,
    Guid ActorUserId) : IRequest<Result<ChannelConnectionDto>>;
