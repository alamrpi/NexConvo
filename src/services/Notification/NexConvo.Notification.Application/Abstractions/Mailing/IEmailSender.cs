namespace NexConvo.Notification.Application.Abstractions.Mailing;

/// <summary>A transactional email to send. Bodies are HTML; sender identity comes from the transport config.</summary>
public sealed record EmailMessage(string ToEmail, string? ToName, string Subject, string HtmlBody);

/// <summary>
/// Sends a single transactional email over a concrete transport (SMTP or Resend). Notification
/// owns this abstraction independently — it does NOT reference Identity's mailing types
/// (Standard 13: the alert-email provider secret lives only in Notification).
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
