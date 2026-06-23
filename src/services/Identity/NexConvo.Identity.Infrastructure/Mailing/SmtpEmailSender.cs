using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using NexConvo.Identity.Application.Abstractions.Mailing;

namespace NexConvo.Identity.Infrastructure.Mailing;

/// <summary>Sends mail over SMTP via MailKit. Built per-send by the resolver with decrypted config.</summary>
public sealed class SmtpEmailSender(SmtpEmailConfig config) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(config.FromName, config.FromAddress));
        mime.To.Add(new MailboxAddress(message.ToName ?? message.ToEmail, message.ToEmail));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        var socketOptions = config.UseSsl ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.None;
        await client.ConnectAsync(config.Host, config.Port, socketOptions, cancellationToken);

        if (!string.IsNullOrWhiteSpace(config.Username))
        {
            await client.AuthenticateAsync(config.Username, config.Password, cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
