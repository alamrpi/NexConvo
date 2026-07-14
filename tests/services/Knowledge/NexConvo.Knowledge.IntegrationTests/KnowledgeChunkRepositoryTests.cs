using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NexConvo.Knowledge.Application.Common.Interfaces;
using NexConvo.Knowledge.Domain.Entities;
using NexConvo.Knowledge.Infrastructure.Multitenancy;
using NexConvo.Knowledge.Infrastructure.Persistence;

namespace NexConvo.Knowledge.IntegrationTests;

[Collection("knowledge-postgres")]
public class KnowledgeChunkRepositoryTests(KnowledgePostgresFixture fixture)
{
    /// <summary>Deterministic unit vector so cosine similarity between two calls is reproducible.</summary>
    private static float[] Embedding(params (int index, float value)[] overrides)
    {
        var vector = new float[1024];
        vector[0] = 1f; // base direction
        foreach (var (index, value) in overrides)
            vector[index] = value;
        return Normalize(vector);
    }

    private static float[] Normalize(float[] vector)
    {
        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        return vector.Select(v => v / norm).ToArray();
    }

    private static async Task<(KnowledgeDocument document, KnowledgeChunkWriter writer)> SeedDocumentAsync(
        KnowledgeDbContext db, Guid tenantId, string contentHash)
    {
        var document = new KnowledgeDocument(tenantId, "handbook.pdf", contentHash);
        db.KnowledgeDocuments.Add(document);
        await db.SaveChangesAsync();
        var writer = new KnowledgeChunkWriter(db, new FixedTenantContext(tenantId), NullLogger<KnowledgeChunkWriter>.Instance);
        return (document, writer);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task SimilaritySearch_ScopedToTenantA_NeverReturnsTenantBsChunks()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var queryVector = Embedding();

        await using (var dbA = fixture.CreateTenantContext(tenantA))
        {
            var (docA, writerA) = await SeedDocumentAsync(dbA, tenantA, $"hash-{Guid.NewGuid():N}");
            await writerA.WriteChunksAsync(docA.Id, 1,
                [new KnowledgeChunkWrite("tenant A chunk", 0, 3, queryVector)], CancellationToken.None);
        }

        await using (var dbB = fixture.CreateTenantContext(tenantB))
        {
            var (docB, writerB) = await SeedDocumentAsync(dbB, tenantB, $"hash-{Guid.NewGuid():N}");
            await writerB.WriteChunksAsync(docB.Id, 1,
                [new KnowledgeChunkWrite("tenant B chunk", 0, 3, queryVector)], CancellationToken.None);
        }

        await using var searchDb = fixture.CreateTenantContext(tenantA);
        var repository = new KnowledgeChunkRepository(searchDb);

        var results = await repository.SimilaritySearchAsync(queryVector, topK: 10, minScore: 0.0, CancellationToken.None);

        results.Should().ContainSingle();
        results[0].Content.Should().Be("tenant A chunk");
    }

    [Fact]
    public async Task SimilaritySearch_RanksClosestVectorFirst()
    {
        var tenantId = Guid.NewGuid();
        var queryVector = Embedding();
        var closeVector = Embedding((1, 0.05f));   // nearly identical direction
        var farVector = Embedding((1, 5f));         // far off-axis

        await using var db = fixture.CreateTenantContext(tenantId);
        var (document, writer) = await SeedDocumentAsync(db, tenantId, $"hash-{Guid.NewGuid():N}");
        await writer.WriteChunksAsync(document.Id, 1,
            [
                new KnowledgeChunkWrite("far chunk", 0, 3, farVector),
                new KnowledgeChunkWrite("close chunk", 1, 3, closeVector),
            ], CancellationToken.None);

        var repository = new KnowledgeChunkRepository(db);
        var results = await repository.SimilaritySearchAsync(queryVector, topK: 10, minScore: 0.0, CancellationToken.None);

        results.Should().HaveCount(2);
        results[0].Content.Should().Be("close chunk");
        results[0].Score.Should().BeGreaterThan(results[1].Score);
    }

    [Fact]
    public async Task SimilaritySearch_FiltersBelowMinScore()
    {
        var tenantId = Guid.NewGuid();
        var queryVector = Embedding();
        var orthogonalVector = Embedding((0, 0f), (2, 1f)); // roughly orthogonal to base direction

        await using var db = fixture.CreateTenantContext(tenantId);
        var (document, writer) = await SeedDocumentAsync(db, tenantId, $"hash-{Guid.NewGuid():N}");
        await writer.WriteChunksAsync(document.Id, 1,
            [new KnowledgeChunkWrite("weak match", 0, 3, orthogonalVector)], CancellationToken.None);

        var repository = new KnowledgeChunkRepository(db);
        var results = await repository.SimilaritySearchAsync(queryVector, topK: 10, minScore: 0.9, CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SimilaritySearch_TruncatesToTopK()
    {
        var tenantId = Guid.NewGuid();
        var queryVector = Embedding();

        await using var db = fixture.CreateTenantContext(tenantId);
        var (document, writer) = await SeedDocumentAsync(db, tenantId, $"hash-{Guid.NewGuid():N}");
        var chunks = Enumerable.Range(0, 5)
            .Select(i => new KnowledgeChunkWrite($"chunk {i}", i, 3, Embedding((1, i * 0.01f))))
            .ToList();
        await writer.WriteChunksAsync(document.Id, 1, chunks, CancellationToken.None);

        var repository = new KnowledgeChunkRepository(db);
        var results = await repository.SimilaritySearchAsync(queryVector, topK: 2, minScore: 0.0, CancellationToken.None);

        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task SimilaritySearch_ExcludesInactiveChunks()
    {
        var tenantId = Guid.NewGuid();
        var queryVector = Embedding();

        await using var db = fixture.CreateTenantContext(tenantId);
        var (document, writer) = await SeedDocumentAsync(db, tenantId, $"hash-{Guid.NewGuid():N}");
        await writer.WriteChunksAsync(document.Id, 1,
            [new KnowledgeChunkWrite("v1 chunk", 0, 3, queryVector)], CancellationToken.None);
        var v1Chunk = await db.KnowledgeChunks.SingleAsync(c => c.DocumentId == document.Id);
        v1Chunk.SetActive(false);
        await db.SaveChangesAsync();

        var repository = new KnowledgeChunkRepository(db);
        var results = await repository.SimilaritySearchAsync(queryVector, topK: 10, minScore: 0.0, CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SimilaritySearch_AfterReEmbedToNewVersion_NeverReturnsThePriorVersionsStaleChunk()
    {
        // D4-5 regression guard: re-embedding writes a NEW DocumentVersion rather than replacing
        // the same version, so this exercises the writer's own version-cutover deactivation
        // end-to-end (not a manual IsActive flip) — proving a stale chunk from a corrected/updated
        // document can no longer surface in retrieval once the new version is written.
        var tenantId = Guid.NewGuid();
        var queryVector = Embedding();

        await using var db = fixture.CreateTenantContext(tenantId);
        var (document, writer) = await SeedDocumentAsync(db, tenantId, $"hash-{Guid.NewGuid():N}");

        await writer.WriteChunksAsync(document.Id, documentVersion: 1,
            [new KnowledgeChunkWrite("stale v1 chunk", 0, 3, queryVector)], CancellationToken.None);
        await writer.WriteChunksAsync(document.Id, documentVersion: 2,
            [new KnowledgeChunkWrite("current v2 chunk", 0, 3, queryVector)], CancellationToken.None);

        var repository = new KnowledgeChunkRepository(db);
        var results = await repository.SimilaritySearchAsync(queryVector, topK: 10, minScore: 0.0, CancellationToken.None);

        results.Should().ContainSingle();
        results[0].Content.Should().Be("current v2 chunk");
    }
}
