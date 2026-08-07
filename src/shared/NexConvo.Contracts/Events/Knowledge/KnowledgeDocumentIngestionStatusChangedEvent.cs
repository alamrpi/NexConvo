namespace NexConvo.Contracts.Events.Knowledge;

/// <summary>
/// Published via the MassTransit outbox after every ingestion status transition
/// (Pending → Processing → Ready / Failed).
/// Uses int for status to keep the Contracts assembly free of domain enums.
/// </summary>
public sealed record KnowledgeDocumentIngestionStatusChangedEvent : IntegrationEvent
{
    public required Guid TenantId { get; init; }
    public required Guid DocumentId { get; init; }

    /// <summary>IngestionStatus as int — avoids domain enum dependency in Contracts.</summary>
    public required int NewStatus { get; init; }

    public required int ChunkCount { get; init; }
    public string? FailureReason { get; init; }
}
