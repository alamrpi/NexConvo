using MediatR;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Application.Features.ChannelConnections.Commands;

/// <summary>
/// Upserts a channel connection for the current tenant.
/// Tenant is resolved from ITenantContext (JWT) — never from the client (Standard 6).
/// Access token is encrypted at rest before persisting (Standard 15).
/// </summary>
public sealed record SaveChannelConnectionCommand(
    ChatChannel Channel,
    string ExternalAccountId,
    string? AccountName,
    string AccessToken,
    string? AppSecret,
    Guid ActorUserId) : IRequest<Result<Guid>>;
