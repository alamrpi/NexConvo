using Hangfire;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Contracts.Events.Chat;

namespace NexConvo.Chat.Infrastructure.Jobs;

/// <summary>
/// Hangfire job stub — transitions a knowledge document through the ingestion status machine.
/// Full chunking / embedding is deferred to Phase 2; this stub exercises the full status
/// machine and event publishing so the API and UI are shippable now.
/// </summary>
public sealed class KnowledgeIngestionJob(
    IChatDbContext db,
    IPublishEndpoint publishEndpoint,
    IBackgroundJobClient backgroundJobClient,
    ILogger<KnowledgeIngestionJob> logger)
    : IKnowledgeIngestionJobRunner
{
    public void EnqueueIngestion(Guid documentId, Guid tenantId)
    {
        backgroundJobClient.Enqueue<KnowledgeIngestionJob>(
            job => job.ExecuteAsync(documentId, tenantId, CancellationToken.None));
    }

    [AutomaticRetry(Attempts = 2)]
    public async Task ExecuteAsync(Guid documentId, Guid tenantId, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Knowledge ingestion job started for document {DocumentId}, tenant {TenantId}",
            documentId, tenantId);

        try
        {
            var document = await db.KnowledgeDocuments
                .FirstOrDefaultAsync(d => d.Id == documentId && d.TenantId == tenantId, cancellationToken);

            if (document is null)
            {
                logger.LogWarning(
                    "Knowledge document {DocumentId} not found for tenant {TenantId} in ingestion job",
                    documentId, tenantId);
                return;
            }

            // Transition: Pending → Processing
            document.SetStatus(DocumentStatus.Processing);
            await db.SaveChangesAsync(cancellationToken);

            await publishEndpoint.Publish(
                new KnowledgeDocumentIngestionStatusChangedEvent
                {
                    TenantId = tenantId,
                    DocumentId = documentId,
                    NewStatus = (int)DocumentStatus.Processing,
                    ChunkCount = 0,
                },
                cancellationToken);

            logger.LogInformation(
                "Knowledge document {DocumentId} transitioned to Processing for tenant {TenantId}",
                documentId, tenantId);

            // Phase 2 stub: simulate extraction + chunking + embedding work
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

            // Transition: Processing → Ready
            document.SetStatus(DocumentStatus.Ready);
            await db.SaveChangesAsync(cancellationToken);

            await publishEndpoint.Publish(
                new KnowledgeDocumentIngestionStatusChangedEvent
                {
                    TenantId = tenantId,
                    DocumentId = documentId,
                    NewStatus = (int)DocumentStatus.Ready,
                    ChunkCount = 0,
                },
                cancellationToken);

            logger.LogInformation(
                "Knowledge document {DocumentId} ingestion completed (stub) for tenant {TenantId}",
                documentId, tenantId);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning(
                "Knowledge ingestion job cancelled for document {DocumentId}, tenant {TenantId}",
                documentId, tenantId);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Knowledge ingestion job failed for document {DocumentId}, tenant {TenantId}",
                documentId, tenantId);

            try
            {
                var document = await db.KnowledgeDocuments
                    .FirstOrDefaultAsync(d => d.Id == documentId && d.TenantId == tenantId,
                        CancellationToken.None);

                if (document is not null)
                {
                    document.SetStatus(DocumentStatus.Failed);
                    await db.SaveChangesAsync(CancellationToken.None);

                    await publishEndpoint.Publish(
                        new KnowledgeDocumentIngestionStatusChangedEvent
                        {
                            TenantId = tenantId,
                            DocumentId = documentId,
                            NewStatus = (int)DocumentStatus.Failed,
                            ChunkCount = 0,
                            FailureReason = ex.Message,
                        },
                        CancellationToken.None);
                }
            }
            catch (Exception innerEx)
            {
                logger.LogError(innerEx,
                    "Failed to update failure status for document {DocumentId}, tenant {TenantId}",
                    documentId, tenantId);
            }

            throw;
        }
    }
}
