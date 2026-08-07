using FluentAssertions;
using Grpc.Core;
using NexConvo.Chat.Infrastructure.ExternalServices;
using NexConvo.Knowledge.Api.Grpc;
using NSubstitute;

namespace NexConvo.Chat.Infrastructure.Tests;

public class KnowledgeRetrievalClientTests
{
    [Fact]
    public async Task SearchAsync_AttachesTenantAndInternalApiKeyHeaders()
    {
        var grpcClient = Substitute.For<KnowledgeRetrieval.KnowledgeRetrievalClient>();
        var tenantId = Guid.NewGuid();

        var reply = new SearchReply();
        reply.Chunks.Add(new Chunk { ChunkId = "c1", DocumentId = "d1", Content = "Refund policy text.", Score = 0.9 });

        Metadata? capturedHeaders = null;

        grpcClient
            .SearchAsync(Arg.Any<SearchRequest>(), Arg.Do<Metadata>(m => capturedHeaders = m), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(_ => new AsyncUnaryCall<SearchReply>(
                Task.FromResult(reply),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => [],
                () => { }));

        var sut = new KnowledgeRetrievalClient(grpcClient, internalApiKey: "test-key");

        var results = await sut.SearchAsync(tenantId, "refund policy", topK: 5, minScore: 0.5, CancellationToken.None);

        results.Should().ContainSingle(r =>
            r.ChunkId == "c1" && r.DocumentId == "d1" && r.Content == "Refund policy text." && Math.Abs(r.Score - 0.9) < 0.0001);

        capturedHeaders.Should().NotBeNull();
        capturedHeaders!.Get("x-tenant-id")!.Value.Should().Be(tenantId.ToString());
        capturedHeaders.Get("x-internal-api-key")!.Value.Should().Be("test-key");
    }

    [Fact]
    public async Task SearchAsync_MapsTopKAndMinScoreOntoRequest()
    {
        var grpcClient = Substitute.For<KnowledgeRetrieval.KnowledgeRetrievalClient>();

        SearchRequest? capturedRequest = null;
        var reply = new SearchReply();

        grpcClient
            .SearchAsync(Arg.Do<SearchRequest>(r => capturedRequest = r), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(_ => new AsyncUnaryCall<SearchReply>(
                Task.FromResult(reply),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => [],
                () => { }));

        var sut = new KnowledgeRetrievalClient(grpcClient, internalApiKey: "test-key");

        await sut.SearchAsync(Guid.NewGuid(), "refund policy", topK: 7, minScore: 0.42, CancellationToken.None);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Query.Should().Be("refund policy");
        capturedRequest.TopK.Should().Be(7);
        capturedRequest.MinScore.Should().Be(0.42);
    }
}
