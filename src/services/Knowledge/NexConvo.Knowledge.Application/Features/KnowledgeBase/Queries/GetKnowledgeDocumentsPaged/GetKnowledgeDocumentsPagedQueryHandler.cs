using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Knowledge.Application.Common.Interfaces;

namespace NexConvo.Knowledge.Application.Features.KnowledgeBase.Queries.GetKnowledgeDocumentsPaged;

public sealed class GetKnowledgeDocumentsPagedQueryHandler(IKnowledgeDbContext db)
    : IRequestHandler<GetKnowledgeDocumentsPagedQuery, Result<PagedResult<KnowledgeDocumentSummaryDto>>>
{
    private const int MaxPageSize = 100;

    public async Task<Result<PagedResult<KnowledgeDocumentSummaryDto>>> Handle(
        GetKnowledgeDocumentsPagedQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, MaxPageSize);

        // RLS scopes to the current tenant; only active (non-deleted) documents are listed.
        var filtered = db.KnowledgeDocuments.Where(d => d.IsActive);

        if (query.Status is not null)
            filtered = filtered.Where(d => d.Status == query.Status);

        if (query.SourceType is not null)
            filtered = filtered.Where(d => d.SourceType == query.SourceType);

        var total = await filtered.CountAsync(cancellationToken);
        var documents = await filtered
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        var items = documents.Select(d => new KnowledgeDocumentSummaryDto(
            d.Id, d.Title, d.FileName, d.SourceType, d.Status, d.ChunkCount, d.FailureReason, d.Version, d.CreatedAt))
            .ToList();

        return Result.Success(new PagedResult<KnowledgeDocumentSummaryDto>(items, total, page, size));
    }
}
