using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;

namespace NexConvo.Identity.Application.Roles;

public sealed record RoleDto(
    Guid Id, string Name, bool IsSystem, bool GrantsAll, IReadOnlyList<string> Permissions, int MemberCount);

/// <summary>All roles for the current tenant (system first), with how many users hold each.</summary>
public sealed record ListRolesQuery : IRequest<Result<IReadOnlyList<RoleDto>>>;

public sealed class ListRolesQueryHandler(IIdentityDbContext db)
    : IRequestHandler<ListRolesQuery, Result<IReadOnlyList<RoleDto>>>
{
    public async Task<Result<IReadOnlyList<RoleDto>>> Handle(ListRolesQuery query, CancellationToken cancellationToken)
    {
        // RLS scopes both tables to the current tenant. PermissionKeys wraps a JSONB backing field,
        // so roles are materialized before projection.
        var roles = await db.Roles.AsNoTracking().ToListAsync(cancellationToken);
        var counts = (await db.UserRoles.AsNoTracking()
                .GroupBy(ur => ur.RoleId)
                .Select(g => new { RoleId = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.RoleId, x => x.Count);

        var dtos = roles
            .OrderByDescending(r => r.IsSystem)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(r => new RoleDto(
                r.Id, r.Name, r.IsSystem, r.GrantsAll, r.PermissionKeys.ToList(), counts.GetValueOrDefault(r.Id)))
            .ToList();

        return Result.Success<IReadOnlyList<RoleDto>>(dtos);
    }
}
