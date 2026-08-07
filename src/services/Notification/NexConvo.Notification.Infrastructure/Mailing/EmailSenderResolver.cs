using Microsoft.Extensions.Options;
using NexConvo.Notification.Application.Abstractions.Mailing;

namespace NexConvo.Notification.Infrastructure.Mailing;

/// <summary>
/// Picks the concrete <see cref="IEmailSender"/> for the configured platform-default provider.
/// Notification has no per-tenant provider concept (Standard 13 — it is the sole holder of the
/// alert email-provider secret and stays stateless), so this is a straight config switch.
/// </summary>
public sealed class EmailSenderResolver(
    IOptions<PlatformDefaultEmailOptions> options,
    ResendEmailSender resendEmailSender,
    SmtpEmailSender smtpEmailSender) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        IEmailSender sender = options.Value.Provider.Equals("Resend", StringComparison.OrdinalIgnoreCase)
            ? resendEmailSender
            : smtpEmailSender;

        return sender.SendAsync(message, cancellationToken);
    }
}
