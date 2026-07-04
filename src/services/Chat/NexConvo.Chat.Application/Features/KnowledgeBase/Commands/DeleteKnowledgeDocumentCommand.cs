using MediatR;
using NexConvo.BuildingBlocks.Results;

namespace NexConvo.Chat.Application.Features.KnowledgeBase.Commands;

/// <summary>Soft-deletes a knowledge document and all its embedding chunks.</summary>
public sealed record DeleteKnowledgeDocumentCommand(
    Guid DocumentId,
    Guid ActorUserId) : IRequest<Result>;
