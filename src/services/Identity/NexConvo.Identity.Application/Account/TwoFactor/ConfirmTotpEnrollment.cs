using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Application.Common;

namespace NexConvo.Identity.Application.Account.TwoFactor;

/// <summary>The recovery codes shown once after 2FA is enabled.</summary>
public sealed record BackupCodesDto(IReadOnlyList<string> BackupCodes);

/// <summary>Confirms enrollment by verifying a code, then enables 2FA + issues backup codes. Authenticated (self).</summary>
public sealed record ConfirmTotpEnrollmentCommand(Guid UserId, string Code) : IRequest<Result<BackupCodesDto>>;

public sealed class ConfirmTotpEnrollmentCommandValidator : AbstractValidator<ConfirmTotpEnrollmentCommand>
{
    public ConfirmTotpEnrollmentCommandValidator() => RuleFor(x => x.Code).NotEmpty();
}

public sealed class ConfirmTotpEnrollmentCommandHandler(
    IIdentityDbContext db,
    ITotpService totp,
    ISecretProtector secretProtector,
    ITenantContext tenant,
    IClock clock,
    IAuditWriter audit,
    ICurrentUserCache cache) : IRequestHandler<ConfirmTotpEnrollmentCommand, Result<BackupCodesDto>>
{
    public async Task<Result<BackupCodesDto>> Handle(ConfirmTotpEnrollmentCommand cmd, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == cmd.UserId, cancellationToken);
        if (user is null)
        {
            return Result<BackupCodesDto>.NotFound("User not found.");
        }

        if (!user.TwoFactorPending || user.EncryptedTotpSecret is null)
        {
            return Result<BackupCodesDto>.Invalid("Start two-factor enrollment first.");
        }

        var secret = secretProtector.Unprotect(user.EncryptedTotpSecret);
        if (!totp.Verify(secret, cmd.Code))
        {
            return Result<BackupCodesDto>.Unauthorized("That code is incorrect. Try again.");
        }

        var (plaintext, hashes) = BackupCodes.Generate(10);
        user.EnableTotp(hashes);
        // Evict any pre-existing (password-only) sessions so they can't bypass the new 2FA.
        await SessionRevocation.RevokeAllAsync(db, cmd.UserId, clock.UtcNow, cancellationToken);
        audit.Add("twofactor.enabled", tenant.TenantId, cmd.UserId, null);
        await db.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(cmd.UserId, cancellationToken);

        return Result.Success(new BackupCodesDto(plaintext));
    }
}
