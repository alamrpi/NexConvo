using FluentAssertions;
using Hangfire;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NexConvo.BuildingBlocks.Ai.Services;
using NexConvo.Contracts.Events.Knowledge;
using NexConvo.Knowledge.Application.Common.Interfaces;
using NexConvo.Knowledge.Domain.Entities;
using NexConvo.Knowledge.Domain.Enums;
using NexConvo.Knowledge.Infrastructure.Chunking;
using NexConvo.Knowledge.Infrastructure.Jobs;
using NexConvo.Knowledge.Infrastructure.Multitenancy;
using NexConvo.Knowledge.Infrastructure.Persistence;
using NexConvo.Knowledge.Infrastructure.TextExtraction;
using NSubstitute;
using Npgsql;
using UglyToad.PdfPig.Writer;

namespace NexConvo.Knowledge.IntegrationTests;

/// <summary>
/// Runs the real ingestion job (S3 download → text extraction → chunking → embedding →
/// transactional chunk write → Ready) against the real RLS-enforced database. The embedding
/// provider is mocked (deterministic 1024-dim vectors) so the suite doesn't require a live
/// bge-m3 server; everything else in the pipeline is real.
/// </summary>
[Collection("knowledge-postgres")]
public class KnowledgeIngestionJobTests(KnowledgePostgresFixture fixture)
{
    private static IEmbeddingProviderFactory BuildEmbeddingProviderFactory()
    {
        var provider = Substitute.For<IEmbeddingProviderService>();
        provider.Dimensions.Returns(1024);
        provider
            .EmbedBatchAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<EmbeddingInputType>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                IReadOnlyList<string> texts = callInfo.ArgAt<IReadOnlyList<string>>(0);
                return (IReadOnlyList<float[]>)texts.Select(DeterministicEmbedding).ToList();
            });

        var factory = Substitute.For<IEmbeddingProviderFactory>();
        factory.GetActiveProvider().Returns(provider);
        return factory;
    }

    private static float[] DeterministicEmbedding(string text)
    {
        var seed = text.GetHashCode();
        var random = new Random(seed);
        var vector = new float[1024];
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)random.NextDouble();
        }
        return vector;
    }

    private static MemoryStream BuildMinimalPdf(string text)
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
        var font = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);
        page.AddText(text, 12, new UglyToad.PdfPig.Core.PdfPoint(25, 700), font);
        return new MemoryStream(builder.Build());
    }

    private KnowledgeIngestionJob BuildJob(
        IPublishEndpoint publishEndpoint,
        IS3StorageService s3Storage,
        IEmbeddingProviderFactory? embeddingProviderFactory = null)
    {
        return new KnowledgeIngestionJob(
            fixture.CreateTenantContext,
            tenantId => new KnowledgeChunkWriter(
                fixture.CreateTenantContext(tenantId),
                new FixedTenantContext(tenantId),
                NullLogger<KnowledgeChunkWriter>.Instance),
            s3Storage,
            new TextExtractorFactory(new PdfTextExtractor(), new DocxTextExtractor(), new PlainTextExtractor()),
            new BengaliAwareChunker(),
            embeddingProviderFactory ?? BuildEmbeddingProviderFactory(),
            publishEndpoint,
            Substitute.For<IBackgroundJobClient>(),
            NullLogger<KnowledgeIngestionJob>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_RealPdf_ProducesChunksWithVectorsAndPublishesStatusEvents()
    {
        var tenantId = Guid.NewGuid();
        Guid documentId;
        const string objectKey = "knowledge/test/doc.pdf";

        await using (var db = fixture.CreateTenantContext(tenantId))
        {
            var document = new KnowledgeDocument(tenantId, "handbook.pdf", $"hash-{Guid.NewGuid():N}");
            document.SetS3ObjectKey(objectKey);
            db.KnowledgeDocuments.Add(document);
            await db.SaveChangesAsync();
            documentId = document.Id;
        }

        var s3Storage = Substitute.For<IS3StorageService>();
        s3Storage
            .GetObjectAsync(tenantId, objectKey, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream>(BuildMinimalPdf(
                "This is a small test document used to validate the real ingestion pipeline. " +
                "It has more than one sentence so the chunker has something to split.")));

        var publishEndpoint = Substitute.For<IPublishEndpoint>();
        var job = BuildJob(publishEndpoint, s3Storage);

        await job.ExecuteAsync(documentId, tenantId, CancellationToken.None);

        await using (var db = fixture.CreateTenantContext(tenantId))
        {
            var document = await db.KnowledgeDocuments.SingleAsync(d => d.Id == documentId);
            document.Status.Should().Be(DocumentStatus.Ready);
            document.ChunkCount.Should().BeGreaterThan(0);

            var chunks = await db.KnowledgeChunks.Where(c => c.DocumentId == documentId).ToListAsync();
            chunks.Should().HaveCount(document.ChunkCount);
            chunks.Should().OnlyContain(c => c.DocumentVersion == document.Version);
        }

        // The stored vectors really are 1024-dim (asserted at the DB, not through EF).
        await using (var conn = new NpgsqlConnection(fixture.SuperuserConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT vector_dims(embedding) FROM knowledge_chunks WHERE knowledge_document_id = @doc", conn);
            cmd.Parameters.AddWithValue("doc", documentId);
            await using var reader = await cmd.ExecuteReaderAsync();
            var sawAnyRow = false;
            while (await reader.ReadAsync())
            {
                sawAnyRow = true;
                reader.GetInt32(0).Should().Be(1024);
            }
            sawAnyRow.Should().BeTrue();
        }

        await publishEndpoint.Received(1).Publish(
            Arg.Is<KnowledgeDocumentIngestionStatusChangedEvent>(e =>
                e.DocumentId == documentId && e.NewStatus == (int)DocumentStatus.Processing),
            Arg.Any<CancellationToken>());
        await publishEndpoint.Received(1).Publish(
            Arg.Is<KnowledgeDocumentIngestionStatusChangedEvent>(e =>
                e.DocumentId == documentId && e.NewStatus == (int)DocumentStatus.Ready && e.ChunkCount > 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_RunTwiceForSameDocument_IsIdempotent_NoDuplicateChunks()
    {
        var tenantId = Guid.NewGuid();
        Guid documentId;
        const string objectKey = "knowledge/test/doc-idempotent.pdf";

        await using (var db = fixture.CreateTenantContext(tenantId))
        {
            var document = new KnowledgeDocument(tenantId, "handbook.pdf", $"hash-{Guid.NewGuid():N}");
            document.SetS3ObjectKey(objectKey);
            db.KnowledgeDocuments.Add(document);
            await db.SaveChangesAsync();
            documentId = document.Id;
        }

        var s3Storage = Substitute.For<IS3StorageService>();
        s3Storage
            .GetObjectAsync(tenantId, objectKey, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream>(BuildMinimalPdf(
                "Idempotency test document. Running the ingestion job twice must not duplicate rows.")));

        var publishEndpoint = Substitute.For<IPublishEndpoint>();

        var firstJob = BuildJob(publishEndpoint, s3Storage);
        await firstJob.ExecuteAsync(documentId, tenantId, CancellationToken.None);

        int chunkCountAfterFirstRun;
        await using (var db = fixture.CreateTenantContext(tenantId))
        {
            chunkCountAfterFirstRun = await db.KnowledgeChunks.CountAsync(c => c.DocumentId == documentId);
        }
        chunkCountAfterFirstRun.Should().BeGreaterThan(0);

        var secondJob = BuildJob(publishEndpoint, s3Storage);
        await secondJob.ExecuteAsync(documentId, tenantId, CancellationToken.None);

        await using (var db = fixture.CreateTenantContext(tenantId))
        {
            var chunkCountAfterSecondRun = await db.KnowledgeChunks.CountAsync(c => c.DocumentId == documentId);
            chunkCountAfterSecondRun.Should().Be(chunkCountAfterFirstRun);

            var document = await db.KnowledgeDocuments.SingleAsync(d => d.Id == documentId);
            document.Status.Should().Be(DocumentStatus.Ready);
            document.ChunkCount.Should().Be(chunkCountAfterFirstRun);
        }
    }

    [Fact]
    public async Task ExecuteAsync_CorruptFileBytes_MarksFailed_WithNoChunksWritten()
    {
        var tenantId = Guid.NewGuid();
        Guid documentId;
        const string objectKey = "knowledge/test/corrupt.pdf";

        await using (var db = fixture.CreateTenantContext(tenantId))
        {
            var document = new KnowledgeDocument(tenantId, "corrupt.pdf", $"hash-{Guid.NewGuid():N}");
            document.SetS3ObjectKey(objectKey);
            db.KnowledgeDocuments.Add(document);
            await db.SaveChangesAsync();
            documentId = document.Id;
        }

        var s3Storage = Substitute.For<IS3StorageService>();
        s3Storage
            .GetObjectAsync(tenantId, objectKey, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream>(
                new MemoryStream([0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09])));

        var publishEndpoint = Substitute.For<IPublishEndpoint>();
        var job = BuildJob(publishEndpoint, s3Storage);

        // AutomaticRetry is a Hangfire filter, not in play when ExecuteAsync is invoked directly —
        // the method still rethrows so Hangfire's real dispatch would retry per [AutomaticRetry(Attempts = 2)].
        var act = () => job.ExecuteAsync(documentId, tenantId, CancellationToken.None);
        await act.Should().ThrowAsync<Exception>();

        await using (var db = fixture.CreateTenantContext(tenantId))
        {
            var document = await db.KnowledgeDocuments.SingleAsync(d => d.Id == documentId);
            document.Status.Should().Be(DocumentStatus.Failed);
            document.FailureReason.Should().NotBeNullOrWhiteSpace();

            var chunkCount = await db.KnowledgeChunks.CountAsync(c => c.DocumentId == documentId);
            chunkCount.Should().Be(0);
        }

        await publishEndpoint.Received(1).Publish(
            Arg.Is<KnowledgeDocumentIngestionStatusChangedEvent>(e =>
                e.DocumentId == documentId && e.NewStatus == (int)DocumentStatus.Failed),
            Arg.Any<CancellationToken>());
    }
}
