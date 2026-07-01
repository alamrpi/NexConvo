namespace NexConvo.Identity.Application.Abstractions.Mailing;

/// <summary>A transactional email to send. Bodies are HTML; sender identity comes from the transport config.</summary>
public sealed record EmailMessage(string ToEmail, string? ToName, string Subject, string HtmlBody);

/// <summary>Sends a single transactional email over a concrete transport (SMTP or Resend).</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves the right <see cref="IEmailSender"/> for the current tenant: the workspace's own
/// configured provider if present + enabled, otherwise the platform-default sender.
/// </summary>
public interface ITenantEmailSenderResolver
{
    Task<IEmailSender> ResolveAsync(CancellationToken cancellationToken = default);
}
