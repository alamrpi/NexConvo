using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Application.Common;

namespace NexConvo.Identity.Application.Authentication.GetCurrentUser;

/// <summary>The user id comes from the validated JWT 'sub' claim; tenant scope from the JWT (RLS).</summary>
public sealed record GetCurrentUserQuery(Guid UserId) : IRequest<Result<CurrentUserDto>>;

public sealed class GetCurrentUserQueryHandler(IIdentityDbContext db)
    : IRequestHandler<GetCurrentUserQuery, Result<CurrentUserDto>>
{
    public async Task<Result<CurrentUserDto>> Handle(GetCurrentUserQuery query, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == query.UserId, cancellationToken);

        if (user is null)
        {
            return Result<CurrentUserDto>.NotFound();
        }

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == user.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result<CurrentUserDto>.NotFound();
        }

        var roleIds = user.Roles.Select(r => r.RoleId).ToList();
        var roles = await db.Roles.Where(r => roleIds.Contains(r.Id)).ToListAsync(cancellationToken);
        var (roleNames, permissions) = RoleProjection.From(roles);

        return Result.Success(new CurrentUserDto(
            user.Id, tenant.Id, tenant.Slug.Value, user.Email.Value, user.FullName, roleNames, permissions));
    }
}
