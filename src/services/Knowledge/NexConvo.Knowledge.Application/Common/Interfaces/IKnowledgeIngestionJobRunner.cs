namespace NexConvo.Knowledge.Application.Common.Interfaces;

/// <summary>
/// Schedules an asynchronous knowledge-document ingestion job (Hangfire / Quartz).
/// Abstracted so the handler doesn't couple to Hangfire directly (Standard 2).
/// </summary>
public interface IKnowledgeIngestionJobRunner
{
    /// <summary>Enqueues a background job to embed the document chunks.</summary>
    void EnqueueIngestion(Guid documentId, Guid tenantId);
}
