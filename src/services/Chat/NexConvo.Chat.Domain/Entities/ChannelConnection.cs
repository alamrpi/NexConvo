using NexConvo.BuildingBlocks.Domain;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Domain.Entities;

/// <summary>
/// Represents a workspace's authenticated connection to an omnichannel platform.
/// One row per (TenantId, Channel, ExternalAccountId) — upserted, never duplicated.
/// The access token is stored encrypted at rest (Standard 15).
/// </summary>
public class ChannelConnection : BaseAggregateRoot
{
    public ChatChannel Channel { get; private set; }

    /// <summary>Platform-specific account/page/bot identifier (plaintext — not a secret).</summary>
    public string ExternalAccountId { get; private set; }

    /// <summary>Human-readable display name returned by the platform (e.g. page name).</summary>
    public string? AccountName { get; private set; }

    /// <summary>AES-encrypted access token. Never stored or returned in plaintext.</summary>
    public string EncryptedAccessToken { get; private set; }

    /// <summary>AES-encrypted app secret (WhatsApp/Facebook/Instagram HMAC signing secret). Nullable for channels that don't use it.</summary>
    public string? EncryptedAppSecret { get; private set; }

    /// <summary>Random token sent to the platform during webhook subscription verification (e.g. Meta hub.verify_token).</summary>
    public string VerifyToken { get; private set; }

    /// <summary>Soft-delete flag — use SetActive(false) instead of hard delete.</summary>
    public bool IsActive { get; private set; }

    public DateTimeOffset? LastTestedAt { get; private set; }
    public ConnectionStatus LastTestStatus { get; private set; } = ConnectionStatus.Untested;
    public string? LastTestError { get; private set; }
    public int? LastTestLatencyMs { get; private set; }

    private ChannelConnection()
    {
        // EF Core
        ExternalAccountId = null!;
        EncryptedAccessToken = null!;
        VerifyToken = null!;
    }

    public ChannelConnection(
        Guid tenantId,
        ChatChannel channel,
        string externalAccountId,
        string? accountName,
        string encryptedAccessToken,
        string? encryptedAppSecret = null,
        bool isActive = true)
    {
        TenantId = tenantId;
        Channel = channel;
        ExternalAccountId = externalAccountId;
        AccountName = accountName;
        EncryptedAccessToken = encryptedAccessToken;
        EncryptedAppSecret = encryptedAppSecret;
        VerifyToken = Guid.NewGuid().ToString("N");
        IsActive = isActive;
    }

    public void UpdateToken(string encryptedAccessToken, string? accountName, string? encryptedAppSecret = null)
    {
        EncryptedAccessToken = encryptedAccessToken;
        AccountName = accountName;
        EncryptedAppSecret = encryptedAppSecret;
    }

    public void SetActive(bool isActive) => IsActive = isActive;

    public void ApplyHealth(ConnectionHealth health)
    {
        LastTestStatus = health.Status;
        LastTestError = health.ErrorMessage;
        LastTestLatencyMs = health.LatencyMs;
        LastTestedAt = DateTimeOffset.UtcNow;
    }

    public void EnsureHealthy()
    {
        if (LastTestStatus != ConnectionStatus.Healthy)
            throw new ConnectionUnhealthyException("channel", LastTestError ?? "Run a connection test in Settings.");
    }
}
