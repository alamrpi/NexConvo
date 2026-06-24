using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Application.Common;
using NexConvo.Identity.Domain.Authentication;
using NexConvo.Identity.Domain.ValueObjects;
using DomainRefreshToken = NexConvo.Identity.Domain.Authentication.RefreshToken;

namespace NexConvo.Identity.Application.Authentication.Login;

public sealed record LoginCommand(string TenantSlug, string Email, string Password)
    : IRequest<Result<LoginResultDto>>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.TenantSlug).NotEmpty();
        RuleFor(x => x.Email).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class LoginCommandHandler(
    IIdentityDbContext db,
    IPasswordHasher passwordHasher,
    IJwtTokenIssuer tokenIssuer,
    IRefreshTokenService refreshTokens,
    ILinkTokenService linkTokens,
    IAmbientTenantSetter tenantSetter,
    IAuditWriter audit,
    IClock clock,
    IOptions<AuthOptions> authOptions) : IRequestHandler<LoginCommand, Result<LoginResultDto>>
{
    // One generic message for every failure mode — no account/tenant enumeration (skill Standard 15).
    private static readonly Result<LoginResultDto> InvalidCredentials =
        Result<LoginResultDto>.Unauthorized("Invalid credentials.");

    private readonly AuthOptions _auth = authOptions.Value;

    public async Task<Result<LoginResultDto>> Handle(LoginCommand cmd, CancellationToken cancellationToken)
    {
        var slug = TenantSlug.Create(cmd.TenantSlug);
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken);
        if (tenant is null)
        {
            return InvalidCredentials;
        }

        tenantSetter.SetTenant(tenant.Id);

        var now = clock.UtcNow;
        var email = Email.Create(cmd.Email);
        var user = await db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        // A locked account is refused regardless of password (brute-force defense), with the same
        // generic response so it can't be used to probe which accounts exist.
        if (user is not null && user.IsLockedOut(now))
        {
            audit.Add("login.locked", tenant.Id, user.Id, null);
            await db.SaveChangesAsync(cancellationToken);
            return InvalidCredentials;
        }

        if (user is null || !passwordHasher.Verify(user.PasswordHash, cmd.Password))
        {
            if (user is not null)
            {
                user.RegisterFailedLogin(now, _auth.LockoutMaxAttempts, TimeSpan.FromMinutes(_auth.LockoutWindowMinutes));
                audit.Add(user.IsLockedOut(now) ? "login.locked" : "login.failed", tenant.Id, user.Id, "bad-password");
                await db.SaveChangesAsync(cancellationToken);
            }

            return InvalidCredentials;
        }

        if (!user.IsActive)
        {
            return Result<LoginResultDto>.Forbidden("Account is disabled.");
        }

        user.ResetFailedLogins(); // password was correct — clear any accrued failures

        // 2FA enabled → don't issue tokens yet; hand back a short-lived single-use challenge.
        if (user.TwoFactorEnabled)
        {
            var challenge = linkTokens.Create(tenant.Id);
            db.OneTimeTokens.Add(OneTimeToken.Issue(
                tenant.Id, user.Id, OneTimeTokenPurpose.TwoFactorChallenge, challenge.TokenHash, now.AddMinutes(5)));
            audit.Add("login.2fa_challenge", tenant.Id, user.Id, null);
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success(new LoginResultDto(null, TwoFactorRequired: true, challenge.Token));
        }

        var roleIds = user.Roles.Select(r => r.RoleId).ToList();
        var roles = await db.Roles.Where(r => roleIds.Contains(r.Id)).ToListAsync(cancellationToken);

        var generated = refreshTokens.Generate(tenant.Id);
        db.RefreshTokens.Add(
            DomainRefreshToken.Issue(tenant.Id, user.Id, generated.TokenHash, generated.ExpiresAt));

        audit.Add("login.success", tenant.Id, user.Id, null);
        await db.SaveChangesAsync(cancellationToken);

        var (roleNames, permissions) = RoleProjection.From(roles);
        var access = tokenIssuer.Issue(user, tenant, roleNames, permissions);
        var tokens = new AuthTokensDto(access.Token, access.ExpiresInSeconds, generated.Token);
        return Result.Success(new LoginResultDto(tokens, TwoFactorRequired: false, ChallengeToken: null));
    }
}
