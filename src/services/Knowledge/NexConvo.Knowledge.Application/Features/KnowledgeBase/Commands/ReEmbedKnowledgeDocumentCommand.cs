using MediatR;
using NexConvo.BuildingBlocks.Results;

namespace NexConvo.Knowledge.Application.Features.KnowledgeBase.Commands;

/// <summary>
/// Increments the document version and re-queues it for embedding.
/// Old chunks are soft-deleted by the ingestion job once new ones are ready.
/// </summary>
public sealed record ReEmbedKnowledgeDocumentCommand(
    Guid DocumentId,
    Guid ActorUserId) : IRequest<Result>;
