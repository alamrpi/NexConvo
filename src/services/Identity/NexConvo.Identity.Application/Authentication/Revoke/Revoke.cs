using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;

namespace NexConvo.Identity.Application.Authentication.Revoke;

/// <summary>Logout: revoke the presented refresh token. Idempotent — always succeeds (no enumeration).</summary>
public sealed record RevokeTokenCommand(string RefreshToken) : IRequest<Result>;

public sealed class RevokeTokenCommandValidator : AbstractValidator<RevokeTokenCommand>
{
    public RevokeTokenCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public sealed class RevokeTokenCommandHandler(
    IIdentityDbContext db,
    IRefreshTokenService refreshTokens,
    IAmbientTenantSetter tenantSetter,
    IAuditWriter audit,
    IClock clock) : IRequestHandler<RevokeTokenCommand, Result>
{
    public async Task<Result> Handle(RevokeTokenCommand cmd, CancellationToken cancellationToken)
    {
        if (refreshTokens.TryGetTenantId(cmd.RefreshToken, out var tenantId))
        {
            tenantSetter.SetTenant(tenantId);

            var hash = refreshTokens.Hash(cmd.RefreshToken);
            var existing = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
            if (existing is not null && existing.IsActive(clock.UtcNow))
            {
                existing.Revoke(clock.UtcNow);
                audit.Add("token.revoke", tenantId, existing.UserId, null);
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        return Result.Success();
    }
}
