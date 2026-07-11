using MediatR;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Knowledge.Domain.Enums;

namespace NexConvo.Knowledge.Application.Features.KnowledgeBase.Queries.GetKnowledgeDocumentById;

public sealed record KnowledgeChunkPreviewDto(Guid Id, int Ordinal, string Content, int TokenCount);

public sealed record KnowledgeDocumentDetailDto(
    Guid Id,
    string Title,
    string FileName,
    SourceType SourceType,
    string? SourceUrl,
    DocumentStatus Status,
    int ChunkCount,
    string? FailureReason,
    int Version,
    string EmbeddingModel,
    int EmbeddingDimensions,
    DateTimeOffset CreatedAt,
    IReadOnlyList<KnowledgeChunkPreviewDto> Chunks,
    int ChunkTotal);

/// <summary>Detail + paginated chunk preview for one knowledge document (Standard 17 on the chunk page).</summary>
public sealed record GetKnowledgeDocumentByIdQuery(Guid DocumentId, int ChunkPage = 1, int ChunkPageSize = 20)
    : IRequest<Result<KnowledgeDocumentDetailDto>>;
