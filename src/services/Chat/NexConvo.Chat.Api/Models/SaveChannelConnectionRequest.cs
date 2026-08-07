using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Api.Models;

/// <summary>
/// Request body for creating or updating a channel connection.
/// Tenant and actor user are resolved server-side from the JWT — never from client input.
/// </summary>
public sealed record SaveChannelConnectionRequest(
    ChatChannel Channel,
    string? ExternalAccountId,
    string? AccountName,
    string AccessToken,
    string? AppSecret);
