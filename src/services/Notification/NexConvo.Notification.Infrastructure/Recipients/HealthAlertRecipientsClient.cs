using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using NexConvo.Notification.Application.Abstractions.Recipients;

namespace NexConvo.Notification.Infrastructure.Recipients;

/// <summary>
/// Calls Identity's internal, shared-secret guarded endpoint to resolve a tenant's active
/// Owner+Admin contacts. The internal API key never appears in logs or exceptions (Standard 13).
/// </summary>
public sealed class HealthAlertRecipientsClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration) : IHealthAlertRecipientsClient
{
    public async Task<IReadOnlyList<RecipientDto>> GetRecipientsAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("identity-internal");

        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"internal/tenants/{tenantId}/health-alert-recipients");
        request.Headers.Add("X-Internal-Api-Key", configuration["Internal:ApiKey"]);

        var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var recipients = await response.Content.ReadFromJsonAsync<RecipientDto[]>(cancellationToken);
        return recipients ?? [];
    }
}
