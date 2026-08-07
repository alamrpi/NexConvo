using MassTransit;
using Microsoft.Extensions.Logging;
using NexConvo.Contracts.Events.Health;
using NexConvo.Notification.Application.Abstractions.Mailing;
using NexConvo.Notification.Application.Abstractions.Recipients;

namespace NexConvo.Notification.Application.Consumers;

/// <summary>
/// Consumes <see cref="IntegrationHealthFailedEvent"/> (published by the Slice-5 health sweeps),
/// resolves the tenant's active Owner+Admin recipients from Identity, and emails each one an
/// alert. Standard 13: never logs a secret; only the sanitized ErrorMessage + non-secret
/// ConfigName reach logs/emails.
/// </summary>
public sealed class IntegrationHealthFailedEventConsumer(
    IHealthAlertRecipientsClient recipientsClient,
    IEmailSender emailSender,
    ILogger<IntegrationHealthFailedEventConsumer> logger)
    : IConsumer<IntegrationHealthFailedEvent>
{
    public async Task Consume(ConsumeContext<IntegrationHealthFailedEvent> context)
    {
        var message = context.Message;

        // Intentionally NOT wrapped in try/catch: if the recipients lookup itself throws, let it
        // bubble so MassTransit's retry policy redelivers the whole message (per the brief).
        var recipients = await recipientsClient.GetRecipientsAsync(message.TenantId, context.CancellationToken);

        if (recipients.Count == 0)
        {
            logger.LogInformation(
                "No active Owner/Admin recipients found for tenant {TenantId} — skipping health alert for {IntegrationKind} config {ConfigId}",
                message.TenantId, message.IntegrationKind, message.ConfigId);
            return;
        }

        var subject = $"[NexConvo] {message.IntegrationKind} connection is failing";
        var htmlBody = BuildHtmlBody(message);

        foreach (var recipient in recipients)
        {
            try
            {
                await emailSender.SendAsync(
                    new EmailMessage(recipient.Email, recipient.Name, subject, htmlBody),
                    context.CancellationToken);
            }
            catch (Exception ex)
            {
                // Per-recipient isolation: one failed send must never stop the others.
                logger.LogError(
                    ex,
                    "Failed to send health-alert email to a recipient for tenant {TenantId}, config {ConfigId}",
                    message.TenantId, message.ConfigId);
            }
        }
    }

    private static string BuildHtmlBody(IntegrationHealthFailedEvent message)
    {
        var sanitizedError = string.IsNullOrWhiteSpace(message.ErrorMessage)
            ? "No further details are available."
            : System.Net.WebUtility.HtmlEncode(message.ErrorMessage);

        return $"""
            <p>The <strong>{System.Net.WebUtility.HtmlEncode(message.IntegrationKind)}</strong> connection
            "<strong>{System.Net.WebUtility.HtmlEncode(message.ConfigName)}</strong>" has stopped working.</p>
            <p>{sanitizedError}</p>
            <p>Review and fix this in your workspace Settings.</p>
            """;
    }
}
