using NexConvo.BuildingBlocks.Domain;
using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Domain.Events;

/// <summary>Published when a KnowledgeDocument status transitions (e.g. Pending → Ready).</summary>
public sealed record KnowledgeDocumentStatusChangedEvent(
    Guid TenantId,
    Guid DocumentId,
    DocumentStatus NewStatus) : IDomainEvent;
