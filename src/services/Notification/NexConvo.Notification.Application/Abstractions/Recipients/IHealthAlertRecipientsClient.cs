namespace NexConvo.Notification.Application.Abstractions.Recipients;

/// <summary>An alert recipient's display name and email, as returned by Identity's internal endpoint.</summary>
public sealed record RecipientDto(string Name, string Email);

/// <summary>
/// Resolves a tenant's active Owner+Admin contacts via Identity's shared-secret guarded internal
/// endpoint. Notification never learns recipients on its own — it always asks the owning service.
/// </summary>
public interface IHealthAlertRecipientsClient
{
    Task<IReadOnlyList<RecipientDto>> GetRecipientsAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
