using MediatR;
using NexConvo.BuildingBlocks.Results;

namespace NexConvo.Chat.Application.Features.KnowledgeBase.Commands;

/// <summary>
/// Records a new knowledge document and enqueues its ingestion job.
/// Content deduplication is performed via SHA-256 hash — same tenant + same hash
/// returns the existing document ID without creating a second row (Standard 18).
/// </summary>
public sealed record UploadKnowledgeDocumentCommand(
    string FileName,
    string ContentHash,
    Guid ActorUserId) : IRequest<Result<Guid>>;
