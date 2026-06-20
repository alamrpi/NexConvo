using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Application.Common;
using NexConvo.Identity.Domain.ValueObjects;
using DomainRefreshToken = NexConvo.Identity.Domain.Authentication.RefreshToken;

namespace NexConvo.Identity.Application.Authentication.Login;

public sealed record LoginCommand(string TenantSlug, string Email, string Password)
    : IRequest<Result<AuthTokensDto>>;

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
    IAmbientTenantSetter tenantSetter,
    IAuditWriter audit) : IRequestHandler<LoginCommand, Result<AuthTokensDto>>
{
    // One generic message for every failure mode — no account/tenant enumeration (skill Standard 15).
    private static readonly Result<AuthTokensDto> InvalidCredentials =
        Result<AuthTokensDto>.Unauthorized("Invalid credentials.");

    public async Task<Result<AuthTokensDto>> Handle(LoginCommand cmd, CancellationToken cancellationToken)
    {
        var slug = TenantSlug.Create(cmd.TenantSlug);
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken);
        if (tenant is null)
        {
            return InvalidCredentials;
        }

        tenantSetter.SetTenant(tenant.Id);

        var email = Email.Create(cmd.Email);
        var user = await db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null || !passwordHasher.Verify(user.PasswordHash, cmd.Password))
        {
            if (user is not null)
            {
                audit.Add("login.failed", tenant.Id, user.Id, "bad-password");
                await db.SaveChangesAsync(cancellationToken);
            }

            return InvalidCredentials;
        }

        if (!user.IsActive)
        {
            return Result<AuthTokensDto>.Forbidden("Account is disabled.");
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
        return Result.Success(new AuthTokensDto(access.Token, access.ExpiresInSeconds, generated.Token));
    }
}
