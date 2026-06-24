using MediatR;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Domain.Roles;

namespace NexConvo.Identity.Application.Roles;

/// <summary>A grantable permission with its module + category (for the role-editor grouping/presets).</summary>
public sealed record PermissionItemDto(string Key, string Module, string Category);

/// <summary>The permissions a custom role may grant (the Owner-only wildcard is excluded).</summary>
public sealed record GetPermissionCatalogQuery : IRequest<Result<IReadOnlyList<PermissionItemDto>>>;

public sealed class GetPermissionCatalogQueryHandler
    : IRequestHandler<GetPermissionCatalogQuery, Result<IReadOnlyList<PermissionItemDto>>>
{
    public Task<Result<IReadOnlyList<PermissionItemDto>>> Handle(
        GetPermissionCatalogQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyList<PermissionItemDto> items = PermissionCatalog.Assignable
            .Select(p => new PermissionItemDto(p.Key, p.Module, p.Category.ToString()))
            .ToList();
        return Task.FromResult(Result.Success(items));
    }
}
