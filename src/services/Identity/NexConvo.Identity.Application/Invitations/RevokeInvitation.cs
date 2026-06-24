using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;

namespace NexConvo.Identity.Application.Invitations;

/// <summary>Cancels a pending invitation. Authenticated (users:invite).</summary>
public sealed record RevokeInvitationCommand(Guid InvitationId, Guid ActorUserId) : IRequest<Result>;

public sealed class RevokeInvitationCommandHandler(IIdentityDbContext db, ITenantContext tenant, IAuditWriter audit)
    : IRequestHandler<RevokeInvitationCommand, Result>
{
    public async Task<Result> Handle(RevokeInvitationCommand cmd, CancellationToken cancellationToken)
    {
        var invitation = await db.Invitations.FirstOrDefaultAsync(i => i.Id == cmd.InvitationId, cancellationToken);
        if (invitation is null || invitation.AcceptedAt is not null)
        {
            return Result.NotFound("Invitation not found.");
        }

        db.Invitations.Remove(invitation);
        audit.Add("invitation.revoked", tenant.TenantId, cmd.ActorUserId, $"email={invitation.Email.Value}");
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
