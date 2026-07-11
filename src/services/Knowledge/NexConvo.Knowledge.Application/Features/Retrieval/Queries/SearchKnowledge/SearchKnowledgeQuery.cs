using MediatR;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Knowledge.Application.Common.Interfaces;

namespace NexConvo.Knowledge.Application.Features.Retrieval.Queries.SearchKnowledge;

/// <summary>
/// RAG retrieval — embeds <paramref name="Text"/> and returns the tenant's nearest knowledge
/// chunks. <paramref name="TopK"/>/<paramref name="MinScore"/> are the per-channel tuning knobs
/// (CHATBOT-ARCHITECTURE.md §7.4): Chat sends a larger TopK, Voice a smaller one for its
/// &lt;700ms budget. The handler clamps TopK server-side regardless of what's requested.
/// </summary>
public sealed record SearchKnowledgeQuery(string Text, int TopK, double MinScore)
    : IRequest<Result<IReadOnlyList<ChunkMatch>>>;
