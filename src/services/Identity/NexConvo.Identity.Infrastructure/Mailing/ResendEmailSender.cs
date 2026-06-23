using System.Net.Http.Headers;
using System.Net.Http.Json;
using NexConvo.Identity.Application.Abstractions.Mailing;

namespace NexConvo.Identity.Infrastructure.Mailing;

/// <summary>
/// Sends mail via the Resend HTTP API. Uses the named "resend" HttpClient (Polly-wrapped,
/// skill Standard 8); the API key is per-tenant so it's set per request, not on the client.
/// </summary>
public sealed class ResendEmailSender(IHttpClientFactory httpClientFactory, ResendEmailConfig config) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
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
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

        var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
