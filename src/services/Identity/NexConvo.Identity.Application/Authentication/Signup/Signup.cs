using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Application.Abstractions.Mailing;
using NexConvo.Identity.Application.Common;
using NexConvo.Identity.Domain.Authentication;
using NexConvo.Identity.Domain.Roles;
using NexConvo.Identity.Domain.Tenants;
using NexConvo.Identity.Domain.Users;
using NexConvo.Identity.Domain.ValueObjects;
using DomainRefreshToken = NexConvo.Identity.Domain.Authentication.RefreshToken;

namespace NexConvo.Identity.Application.Authentication.Signup;

public sealed record SignupCommand(
    string TenantName,
    string TenantSlug,
    string Email,
    string Password,
    string FullName) : IRequest<Result<AuthTokensDto>>;

public sealed class SignupCommandValidator : AbstractValidator<SignupCommand>
{
    public SignupCommandValidator()
    {
        RuleFor(x => x.TenantName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TenantSlug).NotEmpty().Length(3, 40)
            .Matches("^[A-Za-z0-9](-?[A-Za-z0-9])*$")
            .WithMessage("Slug must be 3–40 chars: letters, digits, single hyphens.");
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
    }
}

public sealed class SignupCommandHandler(
    IIdentityDbContext db,
    IPasswordHasher passwordHasher,
    IJwtTokenIssuer tokenIssuer,
    IRefreshTokenService refreshTokens,
    ILinkTokenService linkTokens,
    ITenantEmailSenderResolver senderResolver,
    IAppLinkBuilder appLinks,
    IAmbientTenantSetter tenantSetter,
    IAuditWriter audit,
    IClock clock) : IRequestHandler<SignupCommand, Result<AuthTokensDto>>
{
    public async Task<Result<AuthTokensDto>> Handle(SignupCommand cmd, CancellationToken cancellationToken)
    {
        var slug = TenantSlug.Create(cmd.TenantSlug);
        if (await db.Tenants.AnyAsync(t => t.Slug == slug, cancellationToken))
        {
            return Result<AuthTokensDto>.Conflict("That tenant slug is already taken.");
        }

        var email = Email.Create(cmd.Email);

        var tenant = Tenant.Provision(cmd.TenantName, slug);
        db.Tenants.Add(tenant);

        // New tenant: set context so the RLS-scoped inserts below pass the policy.
        tenantSetter.SetTenant(tenant.Id);

        var roles = Role.DefaultsFor(tenant.Id);
        db.Roles.AddRange(roles);
        var ownerRole = roles.Single(r => r.Name == "Owner");

        var user = User.Register(tenant.Id, email, passwordHasher.Hash(cmd.Password), cmd.FullName);
        user.AssignRole(ownerRole.Id);
        db.Users.Add(user);

        var generated = refreshTokens.Generate(tenant.Id);
        db.RefreshTokens.Add(
            DomainRefreshToken.Issue(tenant.Id, user.Id, generated.TokenHash, generated.ExpiresAt));

        // Email verification token (sent below, best-effort) — login is not blocked on it in v1.
        var verification = linkTokens.Create(tenant.Id);
        db.OneTimeTokens.Add(OneTimeToken.Issue(
            tenant.Id, user.Id, OneTimeTokenPurpose.EmailVerification,
            verification.TokenHash, clock.UtcNow.AddHours(24)));

        audit.Add("tenant.signup", tenant.Id, user.Id, $"slug={slug.Value}");
        await db.SaveChangesAsync(cancellationToken);

        await TrySendVerification(email.Value, user.FullName, appLinks.VerifyEmailLink(verification.Token), cancellationToken);

        var (roleNames, permissions) = RoleProjection.From([ownerRole]);
        var access = tokenIssuer.Issue(user, tenant, roleNames, permissions);
        return Result.Success(new AuthTokensDto(access.Token, access.ExpiresInSeconds, generated.Token));
    }

    private async Task TrySendVerification(string email, string name, string link, CancellationToken ct)
    {
        try
        {
            var sender = await senderResolver.ResolveAsync(ct);
            await sender.SendAsync(AuthEmails.Verification(email, name, link), ct);
        }
        catch
        {
            // Best-effort: account is created; the user can resend verification later.
        }
    }
}
