using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;

namespace NexConvo.Identity.Application.Users;

/// <summary>Disables a user (they can no longer log in). Authenticated (users:manage).</summary>
public sealed record DeactivateUserCommand(Guid UserId, Guid ActorUserId) : IRequest<Result>;

public sealed class DeactivateUserCommandHandler(IIdentityDbContext db, ITenantContext tenant, IAuditWriter audit)
    : IRequestHandler<DeactivateUserCommand, Result>
{
    public async Task<Result> Handle(DeactivateUserCommand cmd, CancellationToken cancellationToken)
    {
        if (cmd.UserId == cmd.ActorUserId)
        {
            return Result.Conflict("You cannot deactivate your own account.");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == cmd.UserId, cancellationToken);
        if (user is null)
        {
            return Result.NotFound("User not found.");
        }

        if (await OwnerGuard.IsOnlyActiveOwnerAsync(db, cmd.UserId, cancellationToken))
        {
            return Result.Conflict("The workspace must keep at least one active Owner.");
        }

        user.Deactivate();
        audit.Add("user.deactivated", tenant.TenantId, cmd.ActorUserId, $"user={cmd.UserId}");
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

/// <summary>Re-enables a disabled user. Authenticated (users:manage).</summary>
public sealed record ReactivateUserCommand(Guid UserId, Guid ActorUserId) : IRequest<Result>;

public sealed class ReactivateUserCommandHandler(IIdentityDbContext db, ITenantContext tenant, IAuditWriter audit)
    : IRequestHandler<ReactivateUserCommand, Result>
{
    public async Task<Result> Handle(ReactivateUserCommand cmd, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == cmd.UserId, cancellationToken);
        if (user is null)
        {
            return Result.NotFound("User not found.");
        }

        user.Reactivate();
        audit.Add("user.reactivated", tenant.TenantId, cmd.ActorUserId, $"user={cmd.UserId}");
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
