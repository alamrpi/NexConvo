using FluentAssertions;
using NexConvo.BuildingBlocks.Ai.Services;
using NexConvo.Knowledge.Application.Common.Interfaces;
using NexConvo.Knowledge.Application.Features.Retrieval.Queries.SearchKnowledge;
using NSubstitute;

namespace NexConvo.Knowledge.Application.UnitTests;

public class SearchKnowledgeQueryHandlerTests
{
    private readonly IEmbeddingProviderFactory _embeddingFactoryMock = Substitute.For<IEmbeddingProviderFactory>();
    private readonly IEmbeddingProviderService _embeddingProviderMock = Substitute.For<IEmbeddingProviderService>();
    private readonly IKnowledgeChunkRepository _repositoryMock = Substitute.For<IKnowledgeChunkRepository>();
    private readonly SearchKnowledgeQueryHandler _handler;

    public SearchKnowledgeQueryHandlerTests()
    {
        _embeddingFactoryMock.GetActiveProvider().Returns(_embeddingProviderMock);
        _handler = new SearchKnowledgeQueryHandler(_embeddingFactoryMock, _repositoryMock);
    }

    [Fact]
    public async Task Handle_EmbedsQueryAsQueryInputType_ThenSearchesWithTheResultingVector()
    {
        var embedding = new float[] { 0.1f, 0.2f };
        _embeddingProviderMock
            .EmbedAsync("refund policy", EmbeddingInputType.Query, Arg.Any<CancellationToken>())
            .Returns(embedding);
        _repositoryMock
            .SimilaritySearchAsync(embedding, Arg.Any<int>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns([new ChunkMatch(Guid.NewGuid(), Guid.NewGuid(), "refund chunk", 0.95)]);

        var result = await _handler.Handle(
            new SearchKnowledgeQuery("refund policy", TopK: 5, MinScore: 0.5), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(m => m.Content == "refund chunk");
        await _embeddingProviderMock.Received(1)
            .EmbedAsync("refund policy", EmbeddingInputType.Query, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesRequestedTopKAndMinScore_ToTheRepository()
    {
        _embeddingProviderMock
            .EmbedAsync(Arg.Any<string>(), Arg.Any<EmbeddingInputType>(), Arg.Any<CancellationToken>())
            .Returns(new float[] { 0.1f });
        _repositoryMock
            .SimilaritySearchAsync(Arg.Any<float[]>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await _handler.Handle(new SearchKnowledgeQuery("q", TopK: 3, MinScore: 0.7), CancellationToken.None);

        await _repositoryMock.Received(1).SimilaritySearchAsync(
            Arg.Any<float[]>(), topK: 3, minScore: 0.7, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0, 1)]     // below floor clamps to 1
    [InlineData(100, 20)]  // above ceiling clamps to server cap
    public async Task Handle_ClampsTopK_ToServerSideBounds(int requestedTopK, int expectedTopK)
    {
        _embeddingProviderMock
            .EmbedAsync(Arg.Any<string>(), Arg.Any<EmbeddingInputType>(), Arg.Any<CancellationToken>())
            .Returns(new float[] { 0.1f });
        _repositoryMock
            .SimilaritySearchAsync(Arg.Any<float[]>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await _handler.Handle(new SearchKnowledgeQuery("q", TopK: requestedTopK, MinScore: 0.0), CancellationToken.None);

        await _repositoryMock.Received(1).SimilaritySearchAsync(
            Arg.Any<float[]>(), topK: expectedTopK, minScore: Arg.Any<double>(), Arg.Any<CancellationToken>());
    }
}
