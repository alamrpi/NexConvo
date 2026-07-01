using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Application.Account.TwoFactor;
using NexConvo.Identity.Application.Common;
using NexConvo.Identity.Domain.Authentication;
using DomainRefreshToken = NexConvo.Identity.Domain.Authentication.RefreshToken;

namespace NexConvo.Identity.Application.Authentication.TwoFactor;

/// <summary>Completes a 2FA login challenge with a TOTP code or a backup code, issuing tokens. Anonymous.</summary>
public sealed record VerifyTwoFactorCommand(string ChallengeToken, string Code) : IRequest<Result<AuthTokensDto>>;

public sealed class VerifyTwoFactorCommandValidator : AbstractValidator<VerifyTwoFactorCommand>
{
    public VerifyTwoFactorCommandValidator()
    {
        RuleFor(x => x.ChallengeToken).NotEmpty();
        RuleFor(x => x.Code).NotEmpty();
    }
}

public sealed class VerifyTwoFactorCommandHandler(
    IIdentityDbContext db,
    ILinkTokenService linkTokens,
    ITotpService totp,
    ISecretProtector secretProtector,
    IJwtTokenIssuer tokenIssuer,
    IRefreshTokenService refreshTokens,
    IAmbientTenantSetter tenantSetter,
    IAuditWriter audit,
    IClock clock) : IRequestHandler<VerifyTwoFactorCommand, Result<AuthTokensDto>>
{
    private static readonly Result<AuthTokensDto> Invalid =
        Result<AuthTokensDto>.Unauthorized("Invalid or expired challenge.");

    public async Task<Result<AuthTokensDto>> Handle(VerifyTwoFactorCommand cmd, CancellationToken cancellationToken)
    {
        if (!linkTokens.TryGetTenantId(cmd.ChallengeToken, out var tenantId))
        {
            return Invalid;
        }

        tenantSetter.SetTenant(tenantId);
        var now = clock.UtcNow;

        var hash = linkTokens.Hash(cmd.ChallengeToken);
        var challenge = await db.OneTimeTokens.FirstOrDefaultAsync(
            t => t.TokenHash == hash && t.Purpose == OneTimeTokenPurpose.TwoFactorChallenge, cancellationToken);
        if (challenge is null || !challenge.IsValid(now))
        {
            return Invalid;
        }

        var user = await db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == challenge.UserId, cancellationToken);
        if (user is null || !user.IsActive || !user.TwoFactorEnabled || user.EncryptedTotpSecret is null)
        {
            return Invalid;
        }

        var secret = secretProtector.Unprotect(user.EncryptedTotpSecret);
        var codeAccepted = totp.Verify(secret, cmd.Code)
            || user.ConsumeBackupCode(BackupCodes.Hash(cmd.Code), now);
        if (!codeAccepted)
        {
            return Invalid;
        }

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return Invalid;
        }

        challenge.Consume(now);
        var generated = refreshTokens.Generate(tenantId);
        db.RefreshTokens.Add(
            DomainRefreshToken.Issue(tenantId, user.Id, generated.TokenHash, generated.ExpiresAt));
        audit.Add("login.2fa_verified", tenantId, user.Id, null);
        await db.SaveChangesAsync(cancellationToken);

        var roleIds = user.Roles.Select(r => r.RoleId).ToList();
        var roles = await db.Roles.Where(r => roleIds.Contains(r.Id)).ToListAsync(cancellationToken);
        var (roleNames, permissions) = RoleProjection.From(roles);
        var access = tokenIssuer.Issue(user, tenant, roleNames, permissions);
        return Result.Success(new AuthTokensDto(access.Token, access.ExpiresInSeconds, generated.Token));
    }
}
