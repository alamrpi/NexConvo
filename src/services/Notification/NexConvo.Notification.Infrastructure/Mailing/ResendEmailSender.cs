using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using NexConvo.Notification.Application.Abstractions.Mailing;

namespace NexConvo.Notification.Infrastructure.Mailing;

/// <summary>
/// Sends mail via the Resend HTTP API using the platform-default (non-tenant) provider config.
/// Uses the named "resend" HttpClient (Polly-wrapped, Standard 8).
/// </summary>
public sealed class ResendEmailSender(
    IHttpClientFactory httpClientFactory,
    IOptions<PlatformDefaultEmailOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        var client = httpClientFactory.CreateClient("resend");

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(new
            {
                from = $"{config.FromName} <{config.FromAddress}>",
                to = new[] { message.ToEmail },
                subject = message.Subject,
                html = message.HtmlBody,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.Resend.ApiKey);

        var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
