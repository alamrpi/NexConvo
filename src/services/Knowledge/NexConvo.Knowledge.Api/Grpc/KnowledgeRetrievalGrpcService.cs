using Grpc.Core;
using MediatR;
using Microsoft.Extensions.Logging;
using NexConvo.Knowledge.Application.Common.Interfaces;
using NexConvo.Knowledge.Application.Features.Retrieval.Queries.SearchKnowledge;

namespace NexConvo.Knowledge.Api.Grpc;

/// <summary>
/// Thin gRPC dispatch (skill Standard 1 — no DbContext/repository call here, MediatR only).
/// Internal-only: <see cref="InternalServiceAuthInterceptor"/> gates every call before it reaches
/// this class and populates <see cref="GrpcCallContext"/>/<c>GrpcTenantContext</c> (RLS scoping),
/// so the tenant here is already trusted metadata, never a value from the request body.
/// </summary>
public sealed class KnowledgeRetrievalGrpcService(
    ISender sender,
    GrpcCallContext callContext,
    ILogger<KnowledgeRetrievalGrpcService> logger)
    : KnowledgeRetrieval.KnowledgeRetrievalBase
{
    public override async Task<SearchReply> Search(SearchRequest request, ServerCallContext context)
    {
        var started = DateTimeOffset.UtcNow;

        var result = await sender.Send(
            new SearchKnowledgeQuery(request.Query, request.TopK, request.MinScore),
            context.CancellationToken);

        if (!result.IsSuccess)
        {
            throw new RpcException(new Status(StatusCode.Internal, result.Error ?? "Search failed."));
        }

        var reply = new SearchReply();
        reply.Chunks.AddRange(result.Value!.Select(MapChunk));

        // No query text logged (Standard 9 — no PII/free text in log messages).
        logger.LogInformation(
            "Knowledge search completed for tenant {TenantId}: requested top_k {RequestedTopK}, {ResultCount} results in {DurationMs}ms",
            callContext.TenantId, request.TopK, reply.Chunks.Count, (DateTimeOffset.UtcNow - started).TotalMilliseconds);

        return reply;
    }

    private static Chunk MapChunk(ChunkMatch match) => new()
    {
        ChunkId = match.ChunkId.ToString(),
        DocumentId = match.DocumentId.ToString(),
        Content = match.Content,
        Score = match.Score,
    };
}
