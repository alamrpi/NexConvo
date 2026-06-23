using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Application.Abstractions.Mailing;
using NexConvo.Identity.Domain.Authentication;

namespace NexConvo.Identity.Application.Authentication.EmailVerification;

/// <summary>Re-issues a verification email for the authenticated user (no-op if already verified).</summary>
public sealed record ResendVerificationCommand(Guid UserId) : IRequest<Result>;

public sealed class ResendVerificationCommandHandler(
    IIdentityDbContext db,
    ILinkTokenService linkTokens,
    ITenantEmailSenderResolver senderResolver,
    IAppLinkBuilder appLinks,
    ITenantContext tenant,
    IAuditWriter audit,
    IClock clock) : IRequestHandler<ResendVerificationCommand, Result>
{
    public async Task<Result> Handle(ResendVerificationCommand cmd, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == cmd.UserId, cancellationToken);
        if (user is null)
        {
            return Result.NotFound();
        }

        if (user.IsEmailVerified)
        {
            return Result.Success();
        }

        var link = linkTokens.Create(tenant.TenantId);
        db.OneTimeTokens.Add(OneTimeToken.Issue(
            tenant.TenantId, user.Id, OneTimeTokenPurpose.EmailVerification,
            link.TokenHash, clock.UtcNow.AddHours(24)));
        audit.Add("email.verification.resend", tenant.TenantId, user.Id, null);
        await db.SaveChangesAsync(cancellationToken);

        await TrySend(user.Email.Value, user.FullName, appLinks.VerifyEmailLink(link.Token), cancellationToken);
        return Result.Success();
    }

    private async Task TrySend(string email, string name, string link, CancellationToken ct)
    {
        try
        {
            var sender = await senderResolver.ResolveAsync(ct);
            await sender.SendAsync(AuthEmails.Verification(email, name, link), ct);
        }
        catch
        {
            // Best-effort — the token is persisted; the user can resend.
        }
    }
}
