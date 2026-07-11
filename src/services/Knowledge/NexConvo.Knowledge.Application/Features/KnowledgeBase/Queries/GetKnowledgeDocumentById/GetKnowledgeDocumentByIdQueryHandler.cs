using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Knowledge.Application.Common.Interfaces;

namespace NexConvo.Knowledge.Application.Features.KnowledgeBase.Queries.GetKnowledgeDocumentById;

public sealed class GetKnowledgeDocumentByIdQueryHandler(IKnowledgeDbContext db)
    : IRequestHandler<GetKnowledgeDocumentByIdQuery, Result<KnowledgeDocumentDetailDto>>
{
    private const int MaxChunkPageSize = 100;

    public async Task<Result<KnowledgeDocumentDetailDto>> Handle(
        GetKnowledgeDocumentByIdQuery query, CancellationToken cancellationToken)
    {
        // RLS scopes to the current tenant — a match here already belongs to this tenant, so a
        // miss (including "exists for another tenant") is indistinguishable from "doesn't exist",
        // which is exactly the 404 semantics we want (never leak cross-tenant existence).
        var document = await db.KnowledgeDocuments
            .FirstOrDefaultAsync(d => d.Id == query.DocumentId && d.IsActive, cancellationToken);

        if (document is null)
            return Result<KnowledgeDocumentDetailDto>.NotFound($"Knowledge document {query.DocumentId} not found.");

        var chunkPage = Math.Max(1, query.ChunkPage);
        var chunkSize = Math.Clamp(query.ChunkPageSize, 1, MaxChunkPageSize);

        var chunkQuery = db.KnowledgeChunks
            .Where(c => c.DocumentId == document.Id && c.IsActive)
            .OrderBy(c => c.Ordinal);

        var chunkTotal = await chunkQuery.CountAsync(cancellationToken);
        var chunks = await chunkQuery
            .Skip((chunkPage - 1) * chunkSize)
            .Take(chunkSize)
            .ToListAsync(cancellationToken);

        var chunkDtos = chunks
            .Select(c => new KnowledgeChunkPreviewDto(c.Id, c.Ordinal, c.Content, c.TokenCount))
            .ToList();

        var dto = new KnowledgeDocumentDetailDto(
            document.Id, document.Title, document.FileName, document.SourceType, document.SourceUrl,
            document.Status, document.ChunkCount, document.FailureReason, document.Version,
            document.EmbeddingModel, document.EmbeddingDimensions, document.CreatedAt,
            chunkDtos, chunkTotal);

        return Result.Success(dto);
    }
}
