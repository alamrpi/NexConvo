using NexConvo.Identity.Domain.WorkspaceSettings;

namespace NexConvo.Identity.Infrastructure.Mailing;

/// <summary>
/// The platform's fallback email sender, used when a workspace hasn't configured its own
/// (and for pre-tenant system mail). Bound from the "Email:Default" config section (env).
/// In dev this points at Mailpit (smtp localhost:1025).
/// </summary>
public sealed class PlatformDefaultEmailOptions
{
    public const string SectionName = "Email:Default";

    public EmailProvider Provider { get; set; } = EmailProvider.Smtp;
    public string FromAddress { get; set; } = "no-reply@nexconvo.local";
    public string FromName { get; set; } = "NexConvo";
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
