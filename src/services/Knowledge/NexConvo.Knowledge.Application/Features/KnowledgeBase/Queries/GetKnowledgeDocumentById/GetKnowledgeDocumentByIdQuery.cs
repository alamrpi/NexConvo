using MediatR;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Knowledge.Domain.Enums;

namespace NexConvo.Knowledge.Application.Features.KnowledgeBase.Queries.GetKnowledgeDocumentById;

public sealed record KnowledgeChunkPreviewDto(Guid Id, int Ordinal, string Content, int TokenCount);

/// <summary>
/// One superseded-or-current chunk generation for a document. <see cref="EmbeddingModel"/> is the
/// document's CURRENT model for every entry — historical versions' chunks don't individually record
/// which model produced them, so this is the best available attribution, not a precise per-version
/// record (a real fix would need KnowledgeChunk to persist its own embedding model/dimensions).
/// </summary>
public sealed record KnowledgeDocumentVersionDto(
    int Version, string EmbeddingModel, int ChunkCount, DateTimeOffset CreatedAt);

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
    int ChunkTotal,
    IReadOnlyList<KnowledgeDocumentVersionDto> VersionHistory);

/// <summary>Detail + paginated chunk preview for one knowledge document (Standard 17 on the chunk page).</summary>
public sealed record GetKnowledgeDocumentByIdQuery(Guid DocumentId, int ChunkPage = 1, int ChunkPageSize = 20)
    : IRequest<Result<KnowledgeDocumentDetailDto>>;
