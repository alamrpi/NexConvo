using MediatR;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Knowledge.Domain.Enums;

namespace NexConvo.Knowledge.Application.Features.KnowledgeBase.Commands;

/// <summary>One Q/A pair supplied by a Faq-sourced upload.</summary>
/// <param name="Question">The FAQ question text.</param>
/// <param name="Answer">The FAQ answer text.</param>
public sealed record FaqPair(string Question, string Answer);

/// <summary>
/// Records a new knowledge document and enqueues its ingestion job. Carries the payload for
/// exactly one of four source shapes (<see cref="SourceType"/>) — File, Url, Text, or Faq —
/// the handler branches on <see cref="SourceType"/> for source-specific validation/handling,
/// but all four converge on one create/dedup/audit/enqueue path.
/// Content deduplication is performed via SHA-256 hash — same tenant + same hash
/// returns the existing document ID without creating a second row (Standard 18).
/// </summary>
public sealed record UploadKnowledgeDocumentCommand(
    SourceType SourceType,
    string Title,
    string ContentHash,
    Guid ActorUserId,
    string? FileName = null,
    Stream? FileStream = null,
    string? SourceUrl = null,
    string? RawText = null,
    IReadOnlyList<FaqPair>? FaqPairs = null) : IRequest<Result<Guid>>;
