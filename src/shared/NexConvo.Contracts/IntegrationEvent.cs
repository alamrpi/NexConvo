namespace NexConvo.Contracts;

/// <summary>
/// Base type for every cross-service message. Published AFTER commit via the MassTransit
/// outbox (skill Standard 10). <see cref="CorrelationId"/> carries the W3C trace id so a
/// single trace spans HTTP → outbox → consumer (skill Standard 9).
/// </summary>
public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>W3C trace id, propagated across the broker for end-to-end correlation.</summary>
    public string? CorrelationId { get; init; }
}
