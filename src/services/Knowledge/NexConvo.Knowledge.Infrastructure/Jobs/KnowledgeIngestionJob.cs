using Hangfire;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NexConvo.Contracts.Events.Knowledge;
using NexConvo.Knowledge.Application.Common.Interfaces;
using NexConvo.Knowledge.Domain.Enums;
using NexConvo.Knowledge.Infrastructure.Multitenancy;
using NexConvo.Knowledge.Infrastructure.Persistence;

namespace NexConvo.Knowledge.Infrastructure.Jobs;

/// <summary>
/// Hangfire job stub — transitions a knowledge document through the ingestion status machine.
/// Full chunking / embedding lands in Slice 3; this stub exercises the full status
/// machine and event publishing so the API and UI are shippable now.
///
/// Tenant scoping: a Hangfire job has no HTTP context, so the DI-scoped DbContext would open a
/// tenant-less connection and RLS would hide every row. The job instead builds its own context
/// on the SERVICE connection string with a <see cref="FixedTenantContext"/> pinned to the job's
/// tenantId argument — RLS stays enforced, scoped to exactly that tenant.
/// </summary>
public sealed class KnowledgeIngestionJob : IKnowledgeIngestionJobRunner
{
    private readonly Func<Guid, IKnowledgeDbContext> _tenantDbFactory;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<KnowledgeIngestionJob> _logger;

    /// <summary>Production entry point: builds a tenant-pinned, RLS-scoped DbContext per execution.</summary>
    public KnowledgeIngestionJob(
        IConfiguration configuration,
        IPublishEndpoint publishEndpoint,
        IBackgroundJobClient backgroundJobClient,
        ILogger<KnowledgeIngestionJob> logger)
        : this(tenantId => CreateTenantScopedDbContext(configuration, tenantId),
               publishEndpoint, backgroundJobClient, logger)
    {
    }

    /// <summary>Test entry point: accepts a context factory so the status machine is testable.</summary>
    public KnowledgeIngestionJob(
        Func<Guid, IKnowledgeDbContext> tenantDbFactory,
        IPublishEndpoint publishEndpoint,
        IBackgroundJobClient backgroundJobClient,
        ILogger<KnowledgeIngestionJob> logger)
    {
        _tenantDbFactory = tenantDbFactory;
        _publishEndpoint = publishEndpoint;
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    private static IKnowledgeDbContext CreateTenantScopedDbContext(IConfiguration configuration, Guid tenantId)
    {
        var connectionString = configuration.GetConnectionString("KnowledgeDb")
            ?? throw new InvalidOperationException("Connection string 'KnowledgeDb' is not configured.");

        var optionsBuilder = new DbContextOptionsBuilder<KnowledgeDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql => npgsql.UseVector());

        return new KnowledgeDbContext(optionsBuilder.Options, new FixedTenantContext(tenantId));
    }

    public void EnqueueIngestion(Guid documentId, Guid tenantId)
    {
        _backgroundJobClient.Enqueue<KnowledgeIngestionJob>(
            job => job.ExecuteAsync(documentId, tenantId, CancellationToken.None));
    }

    [AutomaticRetry(Attempts = 2)]
    public async Task ExecuteAsync(Guid documentId, Guid tenantId, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Knowledge ingestion job started for document {DocumentId}, tenant {TenantId}",
            documentId, tenantId);

        var db = _tenantDbFactory(tenantId);
        try
        {
            var document = await db.KnowledgeDocuments
                .FirstOrDefaultAsync(d => d.Id == documentId && d.TenantId == tenantId, cancellationToken);

            if (document is null)
            {
                _logger.LogWarning(
                    "Knowledge document {DocumentId} not found for tenant {TenantId} in ingestion job",
                    documentId, tenantId);
                return;
            }

            // Transition: Pending → Processing
            document.SetStatus(DocumentStatus.Processing);
            await db.SaveChangesAsync(cancellationToken);

            await _publishEndpoint.Publish(
                new KnowledgeDocumentIngestionStatusChangedEvent
                {
                    TenantId = tenantId,
                    DocumentId = documentId,
                    NewStatus = (int)DocumentStatus.Processing,
                    ChunkCount = 0,
                },
                cancellationToken);

            _logger.LogInformation(
                "Knowledge document {DocumentId} transitioned to Processing for tenant {TenantId}",
                documentId, tenantId);

            // Slice 3 stub: simulate extraction + chunking + embedding work
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

            // Transition: Processing → Ready
            document.SetStatus(DocumentStatus.Ready);
            await db.SaveChangesAsync(cancellationToken);

            await _publishEndpoint.Publish(
                new KnowledgeDocumentIngestionStatusChangedEvent
                {
                    TenantId = tenantId,
                    DocumentId = documentId,
                    NewStatus = (int)DocumentStatus.Ready,
                    ChunkCount = 0,
                },
                cancellationToken);

            _logger.LogInformation(
                "Knowledge document {DocumentId} ingestion completed (stub) for tenant {TenantId}",
                documentId, tenantId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Knowledge ingestion job cancelled for document {DocumentId}, tenant {TenantId}",
                documentId, tenantId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
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

                    await _publishEndpoint.Publish(
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
                _logger.LogError(innerEx,
                    "Failed to update failure status for document {DocumentId}, tenant {TenantId}",
                    documentId, tenantId);
            }

            throw;
        }
        finally
        {
            // The factory news up a context DI never sees — dispose to release the pooled connection.
            if (db is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            else if (db is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
