namespace NexConvo.Contracts.Events.Health;

/// <summary>Raised when a previously-Healthy integration config fails a background re-test.
/// PreviousStatus/newly-failed distinction is implied (it was Healthy). ErrorMessage MUST be
/// sanitized (no secrets). Consumed by the Notification service (Slice 6).</summary>
public sealed record IntegrationHealthFailedEvent(
    Guid TenantId,
    string IntegrationKind,   // "s3" | "ai" | "channel"
    Guid ConfigId,
    string ConfigName,        // e.g. bucket name / provider / channel+account — non-secret label
    string? ErrorMessage,     // sanitized
    string PreviousStatus     // mirrors NexConvo.BuildingBlocks.Domain.Health.ConnectionStatus
                              // (e.g. "Healthy") — kept as string because Contracts has zero
                              // project references and must stay dependency-free.
) : IntegrationEvent;
