using NexConvo.BuildingBlocks.Domain;

namespace NexConvo.Chat.Domain.Entities;

/// <summary>
/// A vector-embedded text chunk produced from a KnowledgeDocument.
/// Soft-deleted when the parent document is deleted or re-embedded.
/// </summary>
public class KnowledgeChunk : BaseAggregateRoot
{
    public Guid DocumentId { get; private set; }

    /// <summary>Chunk text sent to the embedding model.</summary>
    public string Content { get; private set; }

    /// <summary>The document version this chunk was produced from.</summary>
    public int DocumentVersion { get; private set; }

    public bool IsActive { get; private set; }

    private KnowledgeChunk()
    {
        // EF Core
        Content = null!;
    }

    public KnowledgeChunk(Guid tenantId, Guid documentId, string content, int documentVersion)
    {
        TenantId = tenantId;
        DocumentId = documentId;
        Content = content;
        DocumentVersion = documentVersion;
        IsActive = true;
    }

    public void SetActive(bool isActive) => IsActive = isActive;
}
