namespace NexConvo.Notification.Infrastructure.Mailing;

/// <summary>
/// Notification is stateless and platform-default-only (no per-tenant provider — Standard 13:
/// the alert email-provider secret lives only here). Bound from the "Email:Default" config
/// section (env). In dev this points at Mailpit (smtp host:1025).
/// </summary>
public sealed class PlatformDefaultEmailOptions
{
    public const string SectionName = "Email:Default";

    public string Provider { get; set; } = "Smtp"; // "Resend" | "Smtp"
    public string FromAddress { get; set; } = "alerts@nexconvo.local";
    public string FromName { get; set; } = "NexConvo Alerts";
    public SmtpSection Smtp { get; set; } = new();
    public ResendSection Resend { get; set; } = new();

    public sealed class SmtpSection
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 1025;
        public bool UseSsl { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
    }

    public sealed class ResendSection
    {
        public string ApiKey { get; set; } = string.Empty;
    }
}
