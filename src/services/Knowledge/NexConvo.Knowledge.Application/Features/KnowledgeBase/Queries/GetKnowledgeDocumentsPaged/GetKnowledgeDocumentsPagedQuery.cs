using MediatR;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Knowledge.Domain.Enums;

namespace NexConvo.Knowledge.Application.Features.KnowledgeBase.Queries.GetKnowledgeDocumentsPaged;

public sealed record KnowledgeDocumentSummaryDto(
    Guid Id,
    string Title,
    string FileName,
    SourceType SourceType,
    DocumentStatus Status,
    int ChunkCount,
    string? FailureReason,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>A page of the current tenant's knowledge documents (Standard 17 — capped, bounded).</summary>
public sealed record GetKnowledgeDocumentsPagedQuery(
    int Page = 1,
    int PageSize = 20,
    DocumentStatus? Status = null,
    SourceType? SourceType = null)
    : IRequest<Result<PagedResult<KnowledgeDocumentSummaryDto>>>;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
