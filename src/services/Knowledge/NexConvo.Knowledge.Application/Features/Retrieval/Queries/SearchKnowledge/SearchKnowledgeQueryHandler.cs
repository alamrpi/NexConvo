using MediatR;
using NexConvo.BuildingBlocks.Ai.Services;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Knowledge.Application.Common.Interfaces;

namespace NexConvo.Knowledge.Application.Features.Retrieval.Queries.SearchKnowledge;

public sealed class SearchKnowledgeQueryHandler(
    IEmbeddingProviderFactory embeddingProviderFactory,
    IKnowledgeChunkRepository chunkRepository)
    : IRequestHandler<SearchKnowledgeQuery, Result<IReadOnlyList<ChunkMatch>>>
{
    /// <summary>Server-side cap regardless of what a caller requests — protects against a misbehaving/costly caller.</summary>
    private const int MinTopK = 1;
    private const int MaxTopK = 20;

    public async Task<Result<IReadOnlyList<ChunkMatch>>> Handle(SearchKnowledgeQuery query, CancellationToken ct)
    {
        var topK = Math.Clamp(query.TopK, MinTopK, MaxTopK);

        var provider = embeddingProviderFactory.GetActiveProvider();
        var queryEmbedding = await provider.EmbedAsync(query.Text, EmbeddingInputType.Query, ct);

        var matches = await chunkRepository.SimilaritySearchAsync(query.Text, queryEmbedding, topK, query.MinScore, ct);

        return Result.Success(matches);
    }
}
