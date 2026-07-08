using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using NexConvo.Notification.Application.Abstractions.Mailing;

namespace NexConvo.Notification.Infrastructure.Mailing;

/// <summary>Sends mail over SMTP via MailKit — targets Mailpit in dev.</summary>
public sealed class SmtpEmailSender(IOptions<PlatformDefaultEmailOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var config = options.Value;

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(config.FromName, config.FromAddress));
        mime.To.Add(new MailboxAddress(message.ToName ?? message.ToEmail, message.ToEmail));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        var socketOptions = config.Smtp.UseSsl ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.None;
        await client.ConnectAsync(config.Smtp.Host, config.Smtp.Port, socketOptions, cancellationToken);

        if (!string.IsNullOrWhiteSpace(config.Smtp.Username))
        {
            await client.AuthenticateAsync(config.Smtp.Username, config.Smtp.Password, cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
