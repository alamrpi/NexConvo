using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;

namespace NexConvo.Identity.Application.Roles;

/// <summary>Deletes a custom role. Authenticated (roles:manage). Blocked while members hold it.</summary>
public sealed record DeleteRoleCommand(Guid RoleId, Guid ActorUserId) : IRequest<Result>;

public sealed class DeleteRoleCommandHandler(IIdentityDbContext db, ITenantContext tenant, IAuditWriter audit)
    : IRequestHandler<DeleteRoleCommand, Result>
{
    public async Task<Result> Handle(DeleteRoleCommand cmd, CancellationToken cancellationToken)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == cmd.RoleId, cancellationToken);
        if (role is null)
        {
            return Result.NotFound("Role not found.");
        }

        if (role.IsSystem)
        {
            return Result.Forbidden("System roles cannot be deleted.");
        }

        if (await db.UserRoles.AnyAsync(ur => ur.RoleId == role.Id, cancellationToken))
        {
            return Result.Conflict("Reassign the members of this role before deleting it.");
        }

        db.Roles.Remove(role);
        audit.Add("role.deleted", tenant.TenantId, cmd.ActorUserId, $"role={role.Name}");
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
