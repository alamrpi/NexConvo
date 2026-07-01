using NexConvo.BuildingBlocks.Domain;

namespace NexConvo.Identity.Domain.WorkspaceSettings;

/// <summary>
/// A workspace's transactional-email configuration (one row per tenant, RLS-protected).
/// The provider secret (SMTP password / Resend API key) is stored already-encrypted in
/// <see cref="EncryptedSecret"/> — the domain never sees plaintext.
/// </summary>
public sealed class WorkspaceEmailSettings : BaseAggregateRoot
{
    public EmailProvider Provider { get; private set; }
    public string FromName { get; private set; } = string.Empty;
    public string FromAddress { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; }

    public string? SmtpHost { get; private set; }
    public int SmtpPort { get; private set; }
    public string? SmtpUsername { get; private set; }
    public bool SmtpUseSsl { get; private set; }

    /// <summary>Encrypted SMTP password or Resend API key (whichever the current provider uses).</summary>
    public string? EncryptedSecret { get; private set; }

    public DateTimeOffset? LastTestedAt { get; private set; }
    public bool? LastTestSucceeded { get; private set; }

    private WorkspaceEmailSettings() { } // EF

    public static WorkspaceEmailSettings CreateFor(Guid tenantId) => new() { TenantId = tenantId };

    /// <summary><paramref name="encryptedSecret"/> null means "keep the existing secret".</summary>
    public void UpdateSmtp(
        string fromName, string fromAddress, string host, int port, bool useSsl,
        string? username, string? encryptedSecret, bool enabled)
    {
        Provider = EmailProvider.Smtp;
        ApplyCommon(fromName, fromAddress, enabled);
        SmtpHost = host;
        SmtpPort = port;
        SmtpUseSsl = useSsl;
        SmtpUsername = username;
        if (encryptedSecret is not null)
        {
            EncryptedSecret = encryptedSecret;
        }
    }

    /// <summary><paramref name="encryptedSecret"/> null means "keep the existing key".</summary>
    public void UpdateResend(string fromName, string fromAddress, string? encryptedSecret, bool enabled)
    {
        Provider = EmailProvider.Resend;
        ApplyCommon(fromName, fromAddress, enabled);
        SmtpHost = null;
        SmtpPort = 0;
        SmtpUsername = null;
        SmtpUseSsl = false;
        if (encryptedSecret is not null)
        {
            EncryptedSecret = encryptedSecret;
        }
    }

    public void RecordTestResult(bool succeeded, DateTimeOffset at)
    {
        LastTestedAt = at;
        LastTestSucceeded = succeeded;
    }

    public bool HasSecret => !string.IsNullOrEmpty(EncryptedSecret);

    private void ApplyCommon(string fromName, string fromAddress, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(fromAddress))
        {
            throw new DomainException("From address is required.");
        }

        FromName = fromName.Trim();
        FromAddress = fromAddress.Trim();
        IsEnabled = enabled;
    }
}
