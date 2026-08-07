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

        var results = await repository.SimilaritySearchAsync("irrelevant", queryVector, topK: 10, minScore: 0.0, CancellationToken.None);

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
        var results = await repository.SimilaritySearchAsync("irrelevant query text", queryVector, topK: 10, minScore: 0.0, CancellationToken.None);

        results.Should().HaveCount(2);
        results[0].Content.Should().Be("close chunk");
        results[0].Score.Should().BeGreaterThan(results[1].Score);
    }

    [Fact]
    public async Task SimilaritySearch_ShortExactTermQuery_RetrievesChunkDespiteWeakVectorScore()
    {
        // Grounding-hardening exposed this gap live: "roadmap" alone scored 0 vector-only
        // results against a real "Japan .NET Career Roadmap" KB doc, while a long, fully-
        // specific question scored 0.678. The lexical (tsvector) signal must lift an exact
        // keyword match even when the embedding-only cosine score is weak/orthogonal — same
        // orthogonal-vector setup as SimilaritySearch_FiltersBelowMinScore, which proves the
        // vector-only path alone would score this below a realistic MinScore (0.55).
        var tenantId = Guid.NewGuid();
        var queryVector = Embedding();
        var orthogonalVector = Embedding((0, 0f), (2, 1f));

        await using var db = fixture.CreateTenantContext(tenantId);
        var (document, writer) = await SeedDocumentAsync(db, tenantId, $"hash-{Guid.NewGuid():N}");
        await writer.WriteChunksAsync(document.Id, 1,
            [new KnowledgeChunkWrite("The career roadmap recommends targeting senior roles.", 0, 8, orthogonalVector)],
            CancellationToken.None);

        var repository = new KnowledgeChunkRepository(db);

        // Sanity: confirms this fixture reproduces the reported gap — vector-only similarity
        // between queryVector and orthogonalVector is indeed below a realistic MinScore.
        var vectorOnlyScore = (await repository.SimilaritySearchAsync("unrelated words with no lexical overlap", queryVector, topK: 10, minScore: 0.0, CancellationToken.None))
            .Single().Score;
        vectorOnlyScore.Should().BeLessThan(0.55);

        var results = await repository.SimilaritySearchAsync("roadmap", queryVector, topK: 10, minScore: 0.55, CancellationToken.None);

        results.Should().ContainSingle();
        results[0].Content.Should().Contain("roadmap");
    }

    [Fact]
    public async Task SimilaritySearch_GenericQuestionWithFillerWords_StillRetrievesTheChunk()
    {
        // Regression guard: plainto_tsquery/websearch_to_tsquery AND every token together
        // (including filler words like "the"/"about" absent from the KB content), so a natural
        // phrasing like "Tell me about the roadmap" would never match a chunk containing only
        // "roadmap" under naive AND semantics — even though the single word "roadmap" alone
        // matches fine. The lexical query must OR the tokenized terms so any shared word lifts
        // the chunk, not require every word to appear.
        var tenantId = Guid.NewGuid();
        var queryVector = Embedding();
        var orthogonalVector = Embedding((0, 0f), (2, 1f));

        await using var db = fixture.CreateTenantContext(tenantId);
        var (document, writer) = await SeedDocumentAsync(db, tenantId, $"hash-{Guid.NewGuid():N}");
        await writer.WriteChunksAsync(document.Id, 1,
            [new KnowledgeChunkWrite("The career roadmap recommends targeting senior roles.", 0, 8, orthogonalVector)],
            CancellationToken.None);

        var repository = new KnowledgeChunkRepository(db);
        var results = await repository.SimilaritySearchAsync("Tell me about the roadmap", queryVector, topK: 10, minScore: 0.55, CancellationToken.None);

        results.Should().ContainSingle();
        results[0].Content.Should().Contain("roadmap");
    }

    [Fact]
    public async Task SimilaritySearch_LongSpecificQueryWithStrongVectorMatch_StillRetrievesTheChunk()
    {
        // Regression guard (tasks.md 2.4): a query that already scored well under vector-only
        // search must not be demoted below MinScore now that fusion is in the mix — the close/far
        // vector setup mirrors SimilaritySearch_RanksClosestVectorFirst, but this time asserting
        // the winning chunk clears a realistic MinScore end-to-end, not just outranks its sibling.
        var tenantId = Guid.NewGuid();
        var queryVector = Embedding();
        var closeVector = Embedding((1, 0.05f)); // nearly identical direction — strong vector match

        await using var db = fixture.CreateTenantContext(tenantId);
        var (document, writer) = await SeedDocumentAsync(db, tenantId, $"hash-{Guid.NewGuid():N}");
        await writer.WriteChunksAsync(document.Id, 1,
            [new KnowledgeChunkWrite("What career path does the roadmap recommend for a .NET developer in Japan?", 0, 12, closeVector)],
            CancellationToken.None);

        var repository = new KnowledgeChunkRepository(db);
        var results = await repository.SimilaritySearchAsync(
            "What career path does the roadmap recommend for a .NET developer in Japan?",
            queryVector, topK: 10, minScore: 0.55, CancellationToken.None);

        results.Should().ContainSingle();
    }

    [Fact]
    public async Task SimilaritySearch_GenuinelyOutOfScopeQuery_StillReturnsNothing()
    {
        // Regression guard (tasks.md 2.4): hybrid retrieval must not weaken the grounding
        // guarantee — a query with no lexical or semantic overlap with any active chunk must
        // continue to return zero results, exactly like vector-only search did before this change.
        var tenantId = Guid.NewGuid();
        var queryVector = Embedding();
        var orthogonalVector = Embedding((0, 0f), (2, 1f));

        await using var db = fixture.CreateTenantContext(tenantId);
        var (document, writer) = await SeedDocumentAsync(db, tenantId, $"hash-{Guid.NewGuid():N}");
        await writer.WriteChunksAsync(document.Id, 1,
            [new KnowledgeChunkWrite("The career roadmap recommends targeting senior roles.", 0, 8, orthogonalVector)],
            CancellationToken.None);

        var repository = new KnowledgeChunkRepository(db);
        var results = await repository.SimilaritySearchAsync(
            "What is the capital of France?", queryVector, topK: 10, minScore: 0.55, CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SimilaritySearch_BengaliExactTermQuery_RetrievesChunkViaLexicalMatch()
    {
        // tasks.md 2.5: confirms the fix isn't English-only. `simple` config does no stemming, so
        // this only proves exact-term lexical overlap works for Bengali content — not morphological
        // matching (design.md's documented, accepted limitation of the simple-config decision).
        var tenantId = Guid.NewGuid();
        var queryVector = Embedding();
        var orthogonalVector = Embedding((0, 0f), (2, 1f));

        await using var db = fixture.CreateTenantContext(tenantId);
        var (document, writer) = await SeedDocumentAsync(db, tenantId, $"hash-{Guid.NewGuid():N}");
        await writer.WriteChunksAsync(document.Id, 1,
            [new KnowledgeChunkWrite("রিফান্ড নীতি অনুযায়ী ৫ দিনের মধ্যে টাকা ফেরত দেওয়া হয়।", 0, 8, orthogonalVector)],
            CancellationToken.None);

        var repository = new KnowledgeChunkRepository(db);
        var results = await repository.SimilaritySearchAsync(
            "রিফান্ড নীতি", queryVector, topK: 10, minScore: 0.55, CancellationToken.None);

        results.Should().ContainSingle();
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
        var results = await repository.SimilaritySearchAsync("unrelated query with no shared terms", queryVector, topK: 10, minScore: 0.9, CancellationToken.None);

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
        var results = await repository.SimilaritySearchAsync("irrelevant", queryVector, topK: 2, minScore: 0.0, CancellationToken.None);

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
        var results = await repository.SimilaritySearchAsync("irrelevant", queryVector, topK: 10, minScore: 0.0, CancellationToken.None);

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
        var results = await repository.SimilaritySearchAsync("irrelevant", queryVector, topK: 10, minScore: 0.0, CancellationToken.None);

        results.Should().ContainSingle();
        results[0].Content.Should().Be("current v2 chunk");
    }
}
