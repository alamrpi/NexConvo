using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Application.Abstractions.Mailing;
using NexConvo.Identity.Application.Authentication;
using NexConvo.Identity.Application.Common;
using NexConvo.Identity.Domain.Invitations;
using NexConvo.Identity.Domain.ValueObjects;

namespace NexConvo.Identity.Application.Invitations;

/// <summary>Invites an email to join the current tenant with a role. Authenticated (users:invite).</summary>
public sealed record InviteUserCommand(string Email, string RoleName, Guid InvitedByUserId, string InvitedByName)
    : IRequest<Result>, IRequireVerifiedActor
{
    public Guid ActorUserId => InvitedByUserId;
}

public sealed class InviteUserCommandValidator : AbstractValidator<InviteUserCommand>
{
    public InviteUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.RoleName).NotEmpty();
    }
}

public sealed class InviteUserCommandHandler(
    IIdentityDbContext db,
    ILinkTokenService linkTokens,
    ITenantEmailSenderResolver senderResolver,
    IAppLinkBuilder appLinks,
    ITenantContext tenant,
    IAuditWriter audit,
    IClock clock) : IRequestHandler<InviteUserCommand, Result>
{
    public async Task<Result> Handle(InviteUserCommand cmd, CancellationToken cancellationToken)
    {
        var email = Email.Create(cmd.Email);

        var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == cmd.RoleName, cancellationToken);
        if (role is null)
        {
            return Result.Invalid($"Unknown role '{cmd.RoleName}'.");
        }

        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            return Result.Conflict("That email is already a member of this workspace.");
        }

        var link = linkTokens.Create(tenant.TenantId);
        db.Invitations.Add(Invitation.Issue(
            tenant.TenantId, email, role.Id, link.TokenHash, clock.UtcNow.AddDays(7), cmd.InvitedByUserId));
        audit.Add("user.invited", tenant.TenantId, cmd.InvitedByUserId, $"email={email.Value};role={role.Name}");
        await db.SaveChangesAsync(cancellationToken);

        var tenantEntity = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenant.TenantId, cancellationToken);
        var workspaceName = tenantEntity?.Name ?? "your workspace";
        try
        {
            var sender = await senderResolver.ResolveAsync(cancellationToken);
            await sender.SendAsync(
                AuthEmails.Invitation(email.Value, cmd.InvitedByName, workspaceName, appLinks.AcceptInvitationLink(link.Token)),
                cancellationToken);
        }
        catch
        {
            // Best-effort.
        }

        return Result.Success();
    }
}
