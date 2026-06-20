namespace NexConvo.Contracts.Events.Crm;

/// <summary>
/// Published by Core CRM when a deal transitions to Closed-Won. Consumed by the
/// Automation worker, which starts the project-provisioning Saga (skill Standard 10).
/// </summary>
public sealed record DealClosedWonIntegrationEvent : IntegrationEvent
{
    public required Guid DealId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid ContactId { get; init; }
    public required Guid AssignedUserId { get; init; }
    public required decimal DealValue { get; init; }

    /// <summary>ISO 4217 currency code, e.g. BDT, USD.</summary>
    public required string CurrencyCode { get; init; }

    public required DateTimeOffset ClosedAt { get; init; }
}
