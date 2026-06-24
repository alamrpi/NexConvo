using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Application.Abstractions.Mailing;
using NexConvo.Identity.Application.Authentication;

namespace NexConvo.Identity.Application.Invitations;

/// <summary>Re-issues a pending invitation's token and emails the link again. Authenticated (users:invite).</summary>
public sealed record ResendInvitationCommand(Guid InvitationId, Guid ActorUserId, string ActorName) : IRequest<Result>;

public sealed class ResendInvitationCommandHandler(
    IIdentityDbContext db,
    ILinkTokenService linkTokens,
    ITenantEmailSenderResolver senderResolver,
    IAppLinkBuilder appLinks,
    ITenantContext tenant,
    IAuditWriter audit,
    IClock clock) : IRequestHandler<ResendInvitationCommand, Result>
{
    public async Task<Result> Handle(ResendInvitationCommand cmd, CancellationToken cancellationToken)
    {
        var invitation = await db.Invitations.FirstOrDefaultAsync(i => i.Id == cmd.InvitationId, cancellationToken);
        if (invitation is null || invitation.AcceptedAt is not null)
        {
            return Result.NotFound("Invitation not found.");
        }

        var link = linkTokens.Create(tenant.TenantId);
        invitation.Reissue(link.TokenHash, clock.UtcNow.AddDays(7));
        audit.Add("invitation.resent", tenant.TenantId, cmd.ActorUserId, $"email={invitation.Email.Value}");
        await db.SaveChangesAsync(cancellationToken);

        var tenantEntity = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenant.TenantId, cancellationToken);
        var workspaceName = tenantEntity?.Name ?? "your workspace";
        try
        {
            var sender = await senderResolver.ResolveAsync(cancellationToken);
            await sender.SendAsync(
                AuthEmails.Invitation(invitation.Email.Value, cmd.ActorName, workspaceName, appLinks.AcceptInvitationLink(link.Token)),
                cancellationToken);
        }
        catch
        {
            // Best-effort: the invitation is already re-issued and visible in the pending list.
        }

        return Result.Success();
    }
}
