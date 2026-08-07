namespace NexConvo.Contracts.Messages;

/// <summary>Fan-out trigger published by the scheduler; each owning service re-tests its own
/// active configs. Carries no payload (and no secrets) — just EventId/OccurredAt/CorrelationId
/// from the base for tracing.</summary>
public sealed record CheckIntegrationHealthCommand : IntegrationEvent;
