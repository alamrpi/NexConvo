using NexConvo.BuildingBlocks.Domain;
using NexConvo.Knowledge.Domain.Enums;

namespace NexConvo.Knowledge.Domain.Entities;

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

    /// <summary>How the document's content was originally supplied (File/Url/Text/Faq).</summary>
    public SourceType SourceType { get; private set; }

    /// <summary>Display title — for File sources this mirrors FileName unless overridden.</summary>
    public string Title { get; private set; }

    /// <summary>Source URL when SourceType is Url — null otherwise.</summary>
    public string? SourceUrl { get; private set; }

    /// <summary>Set by <see cref="SetFailed"/> when ingestion fails — never document content/PII.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>Embedding model used for the current Ready chunk set.</summary>
    public string EmbeddingModel { get; private set; }

    /// <summary>Embedding vector dimensionality for the current Ready chunk set.</summary>
    public int EmbeddingDimensions { get; private set; }

    /// <summary>Number of chunks written for the current Ready version.</summary>
    public int ChunkCount { get; private set; }

    /// <summary>
    /// Key of the object holding this document's source bytes in the workspace S3 bucket.
    /// Every source type (File/Url/Text/Faq) converges on S3 storage so the ingestion job always
    /// downloads from a single, uniform place — set once at upload time, read by the job.
    /// </summary>
    public string? S3ObjectKey { get; private set; }

    private KnowledgeDocument()
    {
        // EF Core
        FileName = null!;
        ContentHash = null!;
        Title = null!;
        EmbeddingModel = null!;
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
        SourceType = SourceType.File;
        Title = fileName;
        EmbeddingModel = "BAAI/bge-m3";
        EmbeddingDimensions = 1024;
    }

    private KnowledgeDocument(Guid tenantId, string title, string contentHash, SourceType sourceType, string? sourceUrl)
        : this(tenantId, title, contentHash)
    {
        SourceType = sourceType;
        SourceUrl = sourceUrl;
    }

    /// <summary>Creates a document sourced from a fetched URL — <see cref="SourceUrl"/> is recorded for audit/display.</summary>
    public static KnowledgeDocument ForUrl(Guid tenantId, string title, string sourceUrl, string contentHash)
        => new(tenantId, title, contentHash, SourceType.Url, sourceUrl);

    /// <summary>Creates a document sourced from raw pasted text.</summary>
    public static KnowledgeDocument ForText(Guid tenantId, string title, string contentHash)
        => new(tenantId, title, contentHash, SourceType.Text, sourceUrl: null);

    /// <summary>Creates a document sourced from a set of FAQ Q/A pairs (chunked 1:1 by the ingestion job).</summary>
    public static KnowledgeDocument ForFaq(Guid tenantId, string title, string contentHash)
        => new(tenantId, title, contentHash, SourceType.Faq, sourceUrl: null);

    public void SetStatus(DocumentStatus status) => Status = status;

    /// <summary>Records where this document's source bytes live in the workspace S3 bucket.</summary>
    public void SetS3ObjectKey(string s3ObjectKey) => S3ObjectKey = s3ObjectKey;

    public void SetActive(bool isActive) => IsActive = isActive;

    public void IncrementVersionAndResetToPending()
    {
        Version++;
        Status = DocumentStatus.Pending;
    }

    /// <summary>
    /// Marks ingestion as failed. <paramref name="reason"/> must never contain document
    /// content, raw text, or PII — only a diagnostic summary (Standard 9/15).
    /// </summary>
    public void SetFailed(string reason)
    {
        Status = DocumentStatus.Failed;
        FailureReason = reason;
    }

    /// <summary>
    /// Marks ingestion as complete: chunks were written and are queryable for RAG retrieval.
    /// Clears any prior failure reason left over from an earlier failed attempt.
    /// </summary>
    public void SetReady(int chunkCount, string embeddingModel, int embeddingDimensions)
    {
        Status = DocumentStatus.Ready;
        ChunkCount = chunkCount;
        EmbeddingModel = embeddingModel;
        EmbeddingDimensions = embeddingDimensions;
        FailureReason = null;
    }
}
