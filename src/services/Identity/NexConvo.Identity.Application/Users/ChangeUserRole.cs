using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Domain.Users;

namespace NexConvo.Identity.Application.Users;

/// <summary>Sets a user's single role. Authenticated (users:manage).</summary>
public sealed record ChangeUserRoleCommand(Guid UserId, Guid RoleId, Guid ActorUserId) : IRequest<Result>;

public sealed class ChangeUserRoleCommandHandler(IIdentityDbContext db, ITenantContext tenant, IAuditWriter audit)
    : IRequestHandler<ChangeUserRoleCommand, Result>
{
    public async Task<Result> Handle(ChangeUserRoleCommand cmd, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == cmd.UserId, cancellationToken);
        if (user is null)
        {
            return Result.NotFound("User not found.");
        }

        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == cmd.RoleId, cancellationToken);
        if (role is null)
        {
            return Result.Invalid("Unknown role.");
        }

        // Don't let the workspace's last Owner be demoted out of the Owner role.
        var ownerRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Owner", cancellationToken);
        if (ownerRole is not null && cmd.RoleId != ownerRole.Id
            && await OwnerGuard.IsOnlyActiveOwnerAsync(db, cmd.UserId, cancellationToken))
        {
            return Result.Conflict("The workspace must keep at least one Owner.");
        }

        // Manage the join rows explicitly (not via the tracked nav collection) so EF's orphan-delete
        // optimistic row-count check doesn't fight the RLS FORCE policy.
        var existing = await db.UserRoles
            .Where(ur => ur.UserId == cmd.UserId)
            .ToListAsync(cancellationToken);
        db.UserRoles.RemoveRange(existing);
        db.UserRoles.Add(new UserRole(tenant.TenantId, cmd.UserId, cmd.RoleId));

        audit.Add("user.role_changed", tenant.TenantId, cmd.ActorUserId, $"user={cmd.UserId};role={role.Name}");
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
