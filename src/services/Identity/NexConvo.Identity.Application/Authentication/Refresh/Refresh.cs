using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Application.Common;
using DomainRefreshToken = NexConvo.Identity.Domain.Authentication.RefreshToken;

namespace NexConvo.Identity.Application.Authentication.Refresh;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<Result<AuthTokensDto>>;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public sealed class RefreshTokenCommandHandler(
    IIdentityDbContext db,
    IJwtTokenIssuer tokenIssuer,
    IRefreshTokenService refreshTokens,
    IAmbientTenantSetter tenantSetter,
    IAuditWriter audit,
    IClock clock) : IRequestHandler<RefreshTokenCommand, Result<AuthTokensDto>>
{
    private static readonly Result<AuthTokensDto> Invalid =
        Result<AuthTokensDto>.Unauthorized("Invalid refresh token.");

    public async Task<Result<AuthTokensDto>> Handle(RefreshTokenCommand cmd, CancellationToken cancellationToken)
    {
        // The opaque token embeds its tenant id so we can scope RLS without a slug parameter.
        if (!refreshTokens.TryGetTenantId(cmd.RefreshToken, out var tenantId))
        {
            return Invalid;
        }

        tenantSetter.SetTenant(tenantId);

        var hash = refreshTokens.Hash(cmd.RefreshToken);
        var existing = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (existing is null || !existing.IsActive(clock.UtcNow))
        {
            return Invalid;
        }

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        var user = await db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == existing.UserId, cancellationToken);

        if (tenant is null || user is null || !user.IsActive)
        {
            return Invalid;
        }

        var generated = refreshTokens.Generate(tenantId);
        existing.Revoke(clock.UtcNow, generated.TokenHash);   // rotate: old revoked, points to replacement
        db.RefreshTokens.Add(
            DomainRefreshToken.Issue(tenantId, user.Id, generated.TokenHash, generated.ExpiresAt));

        audit.Add("token.refresh", tenantId, user.Id, null);
        await db.SaveChangesAsync(cancellationToken);

        var roleIds = user.Roles.Select(r => r.RoleId).ToList();
        var roles = await db.Roles.Where(r => roleIds.Contains(r.Id)).ToListAsync(cancellationToken);
        var (roleNames, permissions) = RoleProjection.From(roles);
        var access = tokenIssuer.Issue(user, tenant, roleNames, permissions);
        return Result.Success(new AuthTokensDto(access.Token, access.ExpiresInSeconds, generated.Token));
    }
}
