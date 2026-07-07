using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.Chat.Domain.Entities;
using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Application.Features.ChannelConnections.Dtos;

/// <summary>
/// Read model returned by SaveChannelConnectionCommand and GetChannelConnectionsQuery.
/// MaskedAccessToken exposes only the last 4 characters to confirm it is set
/// without ever returning the plaintext or encrypted value (Standard 15).
/// Status is the frontend's 3-value union derived from the richer <see cref="ConnectionStatus"/>
/// health enum via <see cref="FromEntity"/> — the single place this mapping happens.
/// </summary>
public sealed record ChannelConnectionDto(
    Guid Id,
    ChatChannel Channel,
    string ExternalAccountId,
    string? DisplayName,
    string MaskedAccessToken,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string Status,
    string? ErrorMessage,
    ConnectionStatus LastTestStatus,
    DateTimeOffset? LastTestedAt,
    string? LastTestError,
    int? LastTestLatencyMs)
{
    /// <summary>
    /// The single pure mapping from a <see cref="ChannelConnection"/> entity to its DTO, reused by
    /// both the Save handler (post-gate) and the Get query projection so the health→status mapping
    /// never drifts between the two call sites.
    /// </summary>
    public static ChannelConnectionDto FromEntity(ChannelConnection connection) => new(
        connection.Id,
        connection.Channel,
        connection.ExternalAccountId,
        connection.AccountName,
        MaskToken(connection.EncryptedAccessToken),
        connection.IsActive,
        connection.CreatedAt,
        ToStatus(connection.LastTestStatus),
        connection.LastTestStatus is ConnectionStatus.Failed or ConnectionStatus.Degraded
            ? connection.LastTestError
            : null,
        connection.LastTestStatus,
        connection.LastTestedAt,
        connection.LastTestError,
        connection.LastTestLatencyMs);

    /// <summary>Maps the health enum to the frontend's 3-value union string.</summary>
    private static string ToStatus(ConnectionStatus status) => status switch
    {
        ConnectionStatus.Healthy => "connected",
        ConnectionStatus.Failed or ConnectionStatus.Degraded => "error",
        _ => "disconnected",
    };

    /// <summary>Shows "●●●●{last4}" so the UI can confirm a token is set without exposing it.</summary>
    private static string MaskToken(string encryptedToken)
    {
        if (string.IsNullOrEmpty(encryptedToken) || encryptedToken.Length < 4)
            return "●●●●";
        return "●●●●" + encryptedToken[^4..];
    }
}
