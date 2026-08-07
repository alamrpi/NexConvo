using FluentAssertions;
using Grpc.Core;
using Grpc.Net.Client;
using NexConvo.BuildingBlocks.Ai.Services;
using NexConvo.Knowledge.Api.Grpc;
using NexConvo.Knowledge.Application.Common.Interfaces;
using NexConvo.Knowledge.Domain.Entities;
using NexConvo.Knowledge.Infrastructure.Multitenancy;
using NexConvo.Knowledge.Infrastructure.Persistence;
using NSubstitute;

namespace NexConvo.Knowledge.IntegrationTests;

public class KnowledgeRetrievalGrpcServiceTests(KnowledgeApiFactory factory) : IClassFixture<KnowledgeApiFactory>
{
    private static float[] Embedding(params (int index, float value)[] overrides)
    {
        var vector = new float[1024];
        vector[0] = 1f;
        foreach (var (index, value) in overrides)
            vector[index] = value;
        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        return vector.Select(v => v / norm).ToArray();
    }

    private KnowledgeRetrieval.KnowledgeRetrievalClient CreateClient()
    {
        var httpClient = factory.CreateDefaultClient();
        var channel = GrpcChannel.ForAddress(httpClient.BaseAddress!, new GrpcChannelOptions { HttpClient = httpClient });
        return new KnowledgeRetrieval.KnowledgeRetrievalClient(channel);
    }

    private static Metadata AuthenticatedMetadata(Guid tenantId) => new()
    {
        { InternalServiceAuthInterceptor.InternalKeyHeader, KnowledgeApiFactory.TestInternalApiKey },
        { InternalServiceAuthInterceptor.TenantIdHeader, tenantId.ToString() },
    };

    private async Task SeedChunkAsync(Guid tenantId, string content, float[] embedding)
    {
        await using var db = factory.CreateSeedContext(tenantId);
        var document = new KnowledgeDocument(tenantId, "handbook.pdf", $"hash-{Guid.NewGuid():N}");
        db.KnowledgeDocuments.Add(document);
        await db.SaveChangesAsync();

        var writer = new KnowledgeChunkWriter(
            db, new FixedTenantContext(tenantId), Microsoft.Extensions.Logging.Abstractions.NullLogger<KnowledgeChunkWriter>.Instance);
        await writer.WriteChunksAsync(document.Id, 1, [new KnowledgeChunkWrite(content, 0, 3, embedding)], CancellationToken.None);
    }

    [Fact]
    public async Task Search_KnownQuery_RanksExpectedChunkTop1()
    {
        var tenantId = Guid.NewGuid();
        var refundVector = Embedding();
        var unrelatedVector = Embedding((1, 5f));
        await SeedChunkAsync(tenantId, "refund policy: 30 days", refundVector);
        await SeedChunkAsync(tenantId, "unrelated shipping info", unrelatedVector);

        factory.EmbeddingProviderMock
            .EmbedAsync("refund policy", EmbeddingInputType.Query, Arg.Any<CancellationToken>())
            .Returns(refundVector);

        var client = CreateClient();
        var reply = await client.SearchAsync(
            new SearchRequest { Query = "refund policy", TopK = 5, MinScore = 0.0 },
            AuthenticatedMetadata(tenantId));

        reply.Chunks.Should().NotBeEmpty();
        reply.Chunks[0].Content.Should().Be("refund policy: 30 days");
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task Search_TenantAMetadata_NeverReturnsTenantBsChunks()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var vector = Embedding();
        await SeedChunkAsync(tenantA, "tenant A content", vector);
        await SeedChunkAsync(tenantB, "tenant B content", vector);

        factory.EmbeddingProviderMock
            .EmbedAsync(Arg.Any<string>(), EmbeddingInputType.Query, Arg.Any<CancellationToken>())
            .Returns(vector);

        var client = CreateClient();
        var reply = await client.SearchAsync(
            new SearchRequest { Query = "q", TopK = 10, MinScore = 0.0 },
            AuthenticatedMetadata(tenantA));

        reply.Chunks.Should().OnlyContain(c => c.Content == "tenant A content");
    }

    [Fact]
    public async Task Search_SmallTopK_ReturnsFewerResultsThanLargeTopK()
    {
        var tenantId = Guid.NewGuid();
        for (var i = 0; i < 8; i++)
            await SeedChunkAsync(tenantId, $"chunk {i}", Embedding((1, i * 0.001f)));

        var queryVector = Embedding();
        factory.EmbeddingProviderMock
            .EmbedAsync(Arg.Any<string>(), EmbeddingInputType.Query, Arg.Any<CancellationToken>())
            .Returns(queryVector);

        var client = CreateClient();

        var voiceReply = await client.SearchAsync(
            new SearchRequest { Query = "q", TopK = 3, MinScore = 0.0 }, AuthenticatedMetadata(tenantId));
        var chatReply = await client.SearchAsync(
            new SearchRequest { Query = "q", TopK = 8, MinScore = 0.0 }, AuthenticatedMetadata(tenantId));

        voiceReply.Chunks.Should().HaveCount(3);
        chatReply.Chunks.Should().HaveCount(8);
    }

    [Fact]
    public async Task Search_MissingInternalKey_ThrowsUnauthenticated()
    {
        var client = CreateClient();
        var metadata = new Metadata { { InternalServiceAuthInterceptor.TenantIdHeader, Guid.NewGuid().ToString() } };

        var act = () => client.SearchAsync(
            new SearchRequest { Query = "q", TopK = 5, MinScore = 0.0 }, metadata).ResponseAsync;

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(StatusCode.Unauthenticated);
    }

    [Fact]
    public async Task Search_BengaliQuery_ReturnsPlausibleMatch()
    {
        var tenantId = Guid.NewGuid();
        var vector = Embedding();
        await SeedChunkAsync(tenantId, "রিফান্ড নীতি: ৩০ দিন", vector);

        factory.EmbeddingProviderMock
            .EmbedAsync("রিফান্ড নীতি কী?", EmbeddingInputType.Query, Arg.Any<CancellationToken>())
            .Returns(vector);

        var client = CreateClient();
        var reply = await client.SearchAsync(
            new SearchRequest { Query = "রিফান্ড নীতি কী?", TopK = 5, MinScore = 0.0 },
            AuthenticatedMetadata(tenantId));

        reply.Chunks.Should().ContainSingle(c => c.Content == "রিফান্ড নীতি: ৩০ দিন");
    }
}
