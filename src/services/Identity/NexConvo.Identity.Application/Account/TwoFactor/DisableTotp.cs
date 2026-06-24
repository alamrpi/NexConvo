using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;

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
    IAuditWriter audit) : IRequestHandler<DisableTotpCommand, Result>
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
        audit.Add("twofactor.disabled", tenant.TenantId, cmd.UserId, null);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
