using NexConvo.BuildingBlocks.Domain;

namespace NexConvo.Knowledge.Domain.Entities;

/// <summary>
/// A vector-embedded text chunk produced from a KnowledgeDocument.
/// Soft-deleted when the parent document is deleted or re-embedded.
/// The embedding itself is an EF shadow property (infrastructure concern) — see
/// KnowledgeChunkConfiguration.
/// </summary>
public class KnowledgeChunk : BaseAggregateRoot
{
    public Guid DocumentId { get; private set; }

    /// <summary>Chunk text sent to the embedding model.</summary>
    public string Content { get; private set; }

    /// <summary>Zero-based position of this chunk within the document.</summary>
    public int Ordinal { get; private set; }

    /// <summary>Token count of <see cref="Content"/> as measured at chunking time.</summary>
    public int TokenCount { get; private set; }

    /// <summary>The document version this chunk was produced from.</summary>
    public int DocumentVersion { get; private set; }

    public bool IsActive { get; private set; }

    private KnowledgeChunk()
    {
        // EF Core
        Content = null!;
    }

    public KnowledgeChunk(
        Guid tenantId,
        Guid documentId,
        string content,
        int ordinal,
        int tokenCount,
        int documentVersion)
    {
        TenantId = tenantId;
        DocumentId = documentId;
        Content = content;
        Ordinal = ordinal;
        TokenCount = tokenCount;
        DocumentVersion = documentVersion;
        IsActive = true;
    }

    public void SetActive(bool isActive) => IsActive = isActive;
}
