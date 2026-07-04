using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Application.Features.ChannelConnections.Dtos;

/// <summary>
/// Read model returned by GetChannelConnectionsQuery.
/// AccessTokenMasked exposes only the last 4 characters to confirm it is set
/// without ever returning the plaintext or encrypted value (Standard 15).
/// </summary>
public sealed record ChannelConnectionDto(
    Guid Id,
    ChatChannel Channel,
    string ExternalAccountId,
    string? AccountName,
    string AccessTokenMasked,
    bool IsActive);
