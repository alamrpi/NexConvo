using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Application.Abstractions.Mailing;
using NexConvo.Identity.Domain.Authentication;
using NexConvo.Identity.Domain.ValueObjects;

namespace NexConvo.Identity.Application.Authentication.PasswordReset;

/// <summary>Starts a password reset. ALWAYS returns success — no account enumeration (skill Standard 15).</summary>
public sealed record ForgotPasswordCommand(string TenantSlug, string Email) : IRequest<Result>;

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(x => x.TenantSlug).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

public sealed class ForgotPasswordCommandHandler(
    IIdentityDbContext db,
    ILinkTokenService linkTokens,
    ITenantEmailSenderResolver senderResolver,
    IAppLinkBuilder appLinks,
    IAmbientTenantSetter tenantSetter,
    IAuditWriter audit,
    IClock clock) : IRequestHandler<ForgotPasswordCommand, Result>
{
    public async Task<Result> Handle(ForgotPasswordCommand cmd, CancellationToken cancellationToken)
    {
        TenantSlug slug;
        Email email;
        try
        {
            slug = TenantSlug.Create(cmd.TenantSlug);
            email = Email.Create(cmd.Email);
        }
        catch (BuildingBlocks.Domain.DomainException)
        {
            return Result.Success(); // malformed input — say nothing
        }

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken);
        if (tenant is null)
        {
            return Result.Success();
        }

        tenantSetter.SetTenant(tenant.Id);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user is { IsActive: true })
        {
            var link = linkTokens.Create(tenant.Id);
            db.OneTimeTokens.Add(OneTimeToken.Issue(
                tenant.Id, user.Id, OneTimeTokenPurpose.PasswordReset, link.TokenHash, clock.UtcNow.AddMinutes(30)));
            audit.Add("password.reset.requested", tenant.Id, user.Id, null);
            await db.SaveChangesAsync(cancellationToken);

            try
            {
                var sender = await senderResolver.ResolveAsync(cancellationToken);
                await sender.SendAsync(
                    AuthEmails.PasswordReset(user.Email.Value, user.FullName, appLinks.ResetPasswordLink(link.Token)),
                    cancellationToken);
            }
            catch
            {
                // Best-effort.
            }
        }

        return Result.Success();
    }
}
