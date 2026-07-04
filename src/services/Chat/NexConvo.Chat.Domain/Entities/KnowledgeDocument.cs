using NexConvo.BuildingBlocks.Domain;
using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Domain.Entities;

/// <summary>
/// A document uploaded to the workspace knowledge base for RAG retrieval.
/// Content is ingested asynchronously; status tracks the pipeline stage.
/// Duplicate content is detected via SHA-256 hash (Standard 18 / idempotency).
/// </summary>
public class KnowledgeDocument : BaseAggregateRoot
{
    public string FileName { get; private set; }

    /// <summary>SHA-256 hex of the raw file content — used for deduplication.</summary>
    public string ContentHash { get; private set; }

    public DocumentStatus Status { get; private set; }

    /// <summary>Monotonically incremented each time a re-embed is requested.</summary>
    public int Version { get; private set; }

    /// <summary>Soft-delete flag — embedding chunks are also soft-deleted.</summary>
    public bool IsActive { get; private set; }

    private KnowledgeDocument()
    {
        // EF Core
        FileName = null!;
        ContentHash = null!;
    }

    public KnowledgeDocument(
        Guid tenantId,
        string fileName,
        string contentHash)
    {
        TenantId = tenantId;
        FileName = fileName;
        ContentHash = contentHash;
        Status = DocumentStatus.Pending;
        Version = 1;
        IsActive = true;
    }

    public void SetStatus(DocumentStatus status) => Status = status;

    public void SetActive(bool isActive) => IsActive = isActive;

    public void IncrementVersionAndResetToPending()
    {
        Version++;
        Status = DocumentStatus.Pending;
    }
}
