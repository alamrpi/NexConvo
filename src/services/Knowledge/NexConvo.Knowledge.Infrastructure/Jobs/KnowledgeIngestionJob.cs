using Hangfire;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Ai.Services;
using NexConvo.Contracts.Events.Knowledge;
using NexConvo.Knowledge.Application.Common.Interfaces;
using NexConvo.Knowledge.Domain.Entities;
using NexConvo.Knowledge.Domain.Enums;
using NexConvo.Knowledge.Infrastructure.Multitenancy;
using NexConvo.Knowledge.Infrastructure.Persistence;

namespace NexConvo.Knowledge.Infrastructure.Jobs;

/// <summary>
/// Hangfire job — runs the real ingestion pipeline for a knowledge document: downloads its
/// source bytes from S3, extracts plain text (skipped for Faq, whose "text" is already the
/// chunk-ready pair serialization), chunks it (sentence-aware for File/Url/Text, 1-pair-per-chunk
/// for Faq), embeds the chunks in batches, and transactionally replaces the document's chunk set.
///
/// Tenant scoping: a Hangfire job has no HTTP context, so the DI-scoped DbContext would open a
/// tenant-less connection and RLS would hide every row. The job instead builds its own context
/// (and chunk writer) on the SERVICE connection string with a <see cref="FixedTenantContext"/>
/// pinned to the job's tenantId argument — RLS stays enforced, scoped to exactly that tenant.
/// </summary>
public sealed class KnowledgeIngestionJob : IKnowledgeIngestionJobRunner
{
    /// <summary>Chunks are embedded in batches so one call to the provider never carries an unbounded payload.</summary>
    private const int EmbeddingBatchSize = 32;

    private readonly Func<Guid, IKnowledgeDbContext> _tenantDbFactory;
    private readonly Func<Guid, IKnowledgeChunkWriter> _chunkWriterFactory;
    private readonly IS3StorageService _s3Storage;
    private readonly ITextExtractorFactory _textExtractorFactory;
    private readonly IChunker _chunker;
    private readonly IEmbeddingProviderFactory _embeddingProviderFactory;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<KnowledgeIngestionJob> _logger;

    /// <summary>Production entry point: builds a tenant-pinned, RLS-scoped DbContext + chunk writer per execution.</summary>
    public KnowledgeIngestionJob(
        IConfiguration configuration,
        IS3StorageService s3Storage,
        ITextExtractorFactory textExtractorFactory,
        IChunker chunker,
        IEmbeddingProviderFactory embeddingProviderFactory,
        IPublishEndpoint publishEndpoint,
        IBackgroundJobClient backgroundJobClient,
        ILogger<KnowledgeIngestionJob> logger,
        ILoggerFactory loggerFactory)
        : this(
            tenantId => CreateTenantScopedDbContext(configuration, tenantId),
            tenantId => new KnowledgeChunkWriter(
                (KnowledgeDbContext)CreateTenantScopedDbContext(configuration, tenantId),
                new FixedTenantContext(tenantId),
                loggerFactory.CreateLogger<KnowledgeChunkWriter>()),
            s3Storage, textExtractorFactory, chunker, embeddingProviderFactory,
            publishEndpoint, backgroundJobClient, logger)
    {
    }

    /// <summary>Test entry point: accepts context/chunk-writer factories so the pipeline is testable end to end.</summary>
    public KnowledgeIngestionJob(
        Func<Guid, IKnowledgeDbContext> tenantDbFactory,
        Func<Guid, IKnowledgeChunkWriter> chunkWriterFactory,
        IS3StorageService s3Storage,
        ITextExtractorFactory textExtractorFactory,
        IChunker chunker,
        IEmbeddingProviderFactory embeddingProviderFactory,
        IPublishEndpoint publishEndpoint,
        IBackgroundJobClient backgroundJobClient,
        ILogger<KnowledgeIngestionJob> logger)
    {
        _tenantDbFactory = tenantDbFactory;
        _chunkWriterFactory = chunkWriterFactory;
        _s3Storage = s3Storage;
        _textExtractorFactory = textExtractorFactory;
        _chunker = chunker;
        _embeddingProviderFactory = embeddingProviderFactory;
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

            var chunkContents = await ResolveChunkContentsAsync(document, tenantId, cancellationToken);
            var embeddings = await EmbedInBatchesAsync(chunkContents, cancellationToken);

            var provider = _embeddingProviderFactory.GetActiveProvider();
            var chunkWrites = chunkContents
                .Zip(embeddings, (chunk, embedding) =>
                    new KnowledgeChunkWrite(chunk.Content, chunk.Ordinal, chunk.TokenCount, embedding))
                .ToList();

            var chunkWriter = _chunkWriterFactory(tenantId);
            await chunkWriter.WriteChunksAsync(document.Id, document.Version, chunkWrites, cancellationToken);

            // Transition: Processing → Ready
            document.SetReady(chunkWrites.Count, document.EmbeddingModel, provider.Dimensions);
            await db.SaveChangesAsync(cancellationToken);

            await _publishEndpoint.Publish(
                new KnowledgeDocumentIngestionStatusChangedEvent
                {
                    TenantId = tenantId,
                    DocumentId = documentId,
                    NewStatus = (int)DocumentStatus.Ready,
                    ChunkCount = document.ChunkCount,
                },
                cancellationToken);

            _logger.LogInformation(
                "Knowledge document {DocumentId} ingestion completed with {ChunkCount} chunks for tenant {TenantId}",
                documentId, document.ChunkCount, tenantId);
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
                    document.SetFailed(ex.Message);
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

    /// <summary>
    /// Downloads the document's source bytes from S3 and turns them into ordered, token-counted
    /// chunk contents (not yet embedded). Faq-sourced documents bypass the sentence chunker
    /// entirely — the stored "Q: ...\nA: ..." blocks ARE the desired chunk boundary, so each
    /// pair maps 1:1 onto a chunk instead of being re-split by sentence.
    /// </summary>
    private async Task<IReadOnlyList<ChunkResult>> ResolveChunkContentsAsync(
        KnowledgeDocument document, Guid tenantId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(document.S3ObjectKey))
        {
            throw new InvalidOperationException(
                $"Knowledge document {document.Id} has no S3 object key recorded and cannot be ingested.");
        }

        string text;
        await using (var sourceStream = await _s3Storage.GetObjectAsync(tenantId, document.S3ObjectKey, cancellationToken))
        {
            if (document.SourceType is SourceType.Text or SourceType.Faq)
            {
                // The stored bytes for Text/Faq sources already ARE plain text (raw text, or
                // "Q: ...\nA: ..." pair serialization) — no format-specific extraction needed.
                var plainTextExtractor = _textExtractorFactory.GetExtractor(KnowledgeSourceFormat.PlainText);
                text = await plainTextExtractor.ExtractTextAsync(sourceStream, cancellationToken);
            }
            else
            {
                var fileNameHint = document.SourceType == SourceType.Url ? document.SourceUrl : document.FileName;
                var format = _textExtractorFactory.ResolveFormat(contentType: null, fileNameHint);
                var extractor = _textExtractorFactory.GetExtractor(format);
                text = await extractor.ExtractTextAsync(sourceStream, cancellationToken);
            }
        }

        if (document.SourceType == SourceType.Faq)
        {
            return SplitFaqPairsIntoChunks(text);
        }

        return _chunker.Chunk(text, languageHint: null);
    }

    /// <summary>
    /// Splits the "Q: ...\nA: ..." blank-line-delimited serialization written by
    /// UploadKnowledgeDocumentCommandHandler back into one chunk per Q/A pair.
    /// </summary>
    private static IReadOnlyList<ChunkResult> SplitFaqPairsIntoChunks(string text)
    {
        var blocks = text
            .Replace("\r\n", "\n")
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(block => block.Length > 0)
            .ToList();

        var chunks = new List<ChunkResult>(blocks.Count);
        for (var i = 0; i < blocks.Count; i++)
        {
            // Token count isn't load-bearing for Faq pairs (they bypass the token-bounded
            // chunker), but every chunk still carries an approximate count for observability —
            // words is a cheap, dependency-free proxy here.
            var approximateTokenCount = blocks[i].Split(
                (char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
            chunks.Add(new ChunkResult(blocks[i], i, approximateTokenCount));
        }

        return chunks;
    }

    private async Task<List<float[]>> EmbedInBatchesAsync(
        IReadOnlyList<ChunkResult> chunks, CancellationToken cancellationToken)
    {
        var provider = _embeddingProviderFactory.GetActiveProvider();
        var embeddings = new List<float[]>(chunks.Count);

        for (var offset = 0; offset < chunks.Count; offset += EmbeddingBatchSize)
        {
            var batch = chunks.Skip(offset).Take(EmbeddingBatchSize).Select(c => c.Content).ToList();
            var batchEmbeddings = await provider.EmbedBatchAsync(batch, EmbeddingInputType.Document, cancellationToken);
            embeddings.AddRange(batchEmbeddings);
        }

        return embeddings;
    }
}
