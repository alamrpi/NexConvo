using Microsoft.EntityFrameworkCore;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Domain.Users;

namespace NexConvo.Identity.Application.Users;

/// <summary>Guards the invariant that a workspace always keeps at least one active Owner.</summary>
internal static class OwnerGuard
{
    public static async Task<bool> IsOnlyActiveOwnerAsync(
        IIdentityDbContext db, Guid userId, CancellationToken cancellationToken)
    {
        var ownerRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Owner", cancellationToken);
        if (ownerRole is null)
        {
            return false;
        }

        var isOwner = await db.UserRoles.AnyAsync(
            ur => ur.UserId == userId && ur.RoleId == ownerRole.Id, cancellationToken);
        if (!isOwner)
        {
            return false;
        }

        var activeOwners = await (
            from ur in db.UserRoles
            join u in db.Users on ur.UserId equals u.Id
            where ur.RoleId == ownerRole.Id && u.Status == UserStatus.Active
            select ur.UserId).Distinct().CountAsync(cancellationToken);

        return activeOwners <= 1;
    }
}
