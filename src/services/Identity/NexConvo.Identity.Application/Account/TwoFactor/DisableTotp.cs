using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Application.Common;

namespace NexConvo.Identity.Application.Account.TwoFactor;

/// <summary>Disables 2FA after re-confirming the account password. Authenticated (self).</summary>
public sealed record DisableTotpCommand(Guid UserId, string Password) : IRequest<Result>;

public sealed class DisableTotpCommandValidator : AbstractValidator<DisableTotpCommand>
{
    public DisableTotpCommandValidator() => RuleFor(x => x.Password).NotEmpty();
}

public sealed class DisableTotpCommandHandler(
    IIdentityDbContext db,
    IPasswordHasher passwordHasher,
    ITenantContext tenant,
    IClock clock,
    IAuditWriter audit,
    ICurrentUserCache cache) : IRequestHandler<DisableTotpCommand, Result>
{
    public async Task<Result> Handle(DisableTotpCommand cmd, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == cmd.UserId, cancellationToken);
        if (user is null)
        {
            return Result.NotFound("User not found.");
        }

        if (!user.TwoFactorEnabled)
        {
            return Result.Success(); // already off — idempotent
        }

        if (!passwordHasher.Verify(user.PasswordHash, cmd.Password))
        {
            return Result.Unauthorized("Password is incorrect.");
        }

        user.DisableTotp();
        // Removing a second factor is a security downgrade — evict every existing session.
        await SessionRevocation.RevokeAllAsync(db, cmd.UserId, clock.UtcNow, cancellationToken);
        audit.Add("twofactor.disabled", tenant.TenantId, cmd.UserId, null);
        await db.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(cmd.UserId, cancellationToken);

        return Result.Success();
    }
}
