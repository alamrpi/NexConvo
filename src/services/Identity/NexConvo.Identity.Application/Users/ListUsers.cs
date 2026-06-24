using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Application.Common;

namespace NexConvo.Identity.Application.Users;

public sealed record UserListItemDto(
    Guid Id,
    string Email,
    string FullName,
    string Status,
    bool EmailVerified,
    Guid? RoleId,
    string? RoleName,
    DateTimeOffset CreatedAt);

/// <summary>A page of the current tenant's users with their (single) role.</summary>
public sealed record ListUsersQuery(int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<UserListItemDto>>>;

public sealed class ListUsersQueryHandler(IIdentityDbContext db)
    : IRequestHandler<ListUsersQuery, Result<PagedResult<UserListItemDto>>>
{
    private const int MaxPageSize = 100;

    public async Task<Result<PagedResult<UserListItemDto>>> Handle(
        ListUsersQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, MaxPageSize);

        // RLS scopes to the current tenant.
        var total = await db.Users.CountAsync(cancellationToken);
        var users = await db.Users.AsNoTracking()
            .Include(u => u.Roles)
            .OrderBy(u => u.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        var roleIds = users.SelectMany(u => u.Roles.Select(r => r.RoleId)).Distinct().ToList();
        var roleNames = await db.Roles
            .Where(r => roleIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Name, cancellationToken);

        var items = users.Select(u =>
        {
            var roleId = u.Roles.Select(r => (Guid?)r.RoleId).FirstOrDefault();
            return new UserListItemDto(
                u.Id, u.Email.Value, u.FullName, u.Status.ToString(), u.IsEmailVerified,
                roleId, roleId is null ? null : roleNames.GetValueOrDefault(roleId.Value), u.CreatedAt);
        }).ToList();

        return Result.Success(new PagedResult<UserListItemDto>(items, total, page, size));
    }
}
