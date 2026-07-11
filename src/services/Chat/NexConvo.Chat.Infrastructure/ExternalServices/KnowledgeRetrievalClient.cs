using System.Diagnostics;
using Grpc.Core;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Knowledge.Api.Grpc;

namespace NexConvo.Chat.Infrastructure.ExternalServices;

/// <summary>
/// Chat's client for Knowledge's internal-only <c>KnowledgeRetrieval.Search</c> gRPC service.
/// Auth is a shared secret (<c>x-internal-api-key</c>, matching Knowledge's
/// <c>InternalServiceAuthInterceptor</c>) plus tenant/correlation metadata — never a proto field
/// (skill Standard 6). Query embedding happens entirely server-side in Knowledge.
/// </summary>
public sealed class KnowledgeRetrievalClient(
    KnowledgeRetrieval.KnowledgeRetrievalClient grpcClient,
    ITenantContext tenantContext,
    string internalApiKey) : IKnowledgeRetrievalClient
{
    public async Task<IReadOnlyList<KnowledgeChunkMatch>> SearchAsync(
        string query, int topK, double minScore, CancellationToken cancellationToken)
    {
        var headers = new Metadata
        {
            { "x-internal-api-key", internalApiKey },
            { "x-tenant-id", tenantContext.TenantId.ToString() },
        };

        if (Activity.Current?.Id is { } traceparent)
        {
            headers.Add("traceparent", traceparent);
        }

        var request = new SearchRequest { Query = query, TopK = topK, MinScore = minScore };
        var reply = await grpcClient.SearchAsync(request, headers, cancellationToken: cancellationToken);

        return reply.Chunks
            .Select(c => new KnowledgeChunkMatch(c.ChunkId, c.DocumentId, c.Content, c.Score))
            .ToList();
    }
}
