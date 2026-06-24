using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;

namespace NexConvo.Identity.Application.Account.TwoFactor;

/// <summary>The TOTP secret + provisioning URI, returned once during enrollment.</summary>
public sealed record TotpEnrollmentDto(string Secret, string OtpAuthUri);

/// <summary>Begins 2FA enrollment: generates + stores (encrypted) a pending secret. Authenticated (self).</summary>
public sealed record StartTotpEnrollmentCommand(Guid UserId) : IRequest<Result<TotpEnrollmentDto>>;

public sealed class StartTotpEnrollmentCommandHandler(
    IIdentityDbContext db,
    ITotpService totp,
    ISecretProtector secretProtector,
    ITenantContext tenant,
    IAuditWriter audit) : IRequestHandler<StartTotpEnrollmentCommand, Result<TotpEnrollmentDto>>
{
    public async Task<Result<TotpEnrollmentDto>> Handle(StartTotpEnrollmentCommand cmd, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == cmd.UserId, cancellationToken);
        if (user is null)
        {
            return Result<TotpEnrollmentDto>.NotFound("User not found.");
        }

        if (user.TwoFactorEnabled)
        {
            return Result<TotpEnrollmentDto>.Conflict("Two-factor authentication is already enabled.");
        }

        var secret = totp.GenerateSecret();
        user.SetPendingTotpSecret(secretProtector.Protect(secret));
        audit.Add("twofactor.enroll_started", tenant.TenantId, cmd.UserId, null);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(new TotpEnrollmentDto(secret, totp.BuildOtpAuthUri(secret, user.Email.Value)));
    }
}
