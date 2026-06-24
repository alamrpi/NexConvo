using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;

namespace NexConvo.Identity.Application.Account;

/// <summary>Changes the current user's password after verifying the current one. Authenticated (self).</summary>
public sealed record ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword) : IRequest<Result>;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
    }
}

public sealed class ChangePasswordCommandHandler(
    IIdentityDbContext db,
    IPasswordHasher passwordHasher,
    IClock clock,
    ITenantContext tenant,
    IAuditWriter audit) : IRequestHandler<ChangePasswordCommand, Result>
{
    public async Task<Result> Handle(ChangePasswordCommand cmd, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == cmd.UserId, cancellationToken);
        if (user is null)
        {
            return Result.NotFound("User not found.");
        }

        if (!passwordHasher.Verify(user.PasswordHash, cmd.CurrentPassword))
        {
            return Result.Unauthorized("Current password is incorrect.");
        }

        user.SetPasswordHash(passwordHasher.Hash(cmd.NewPassword));

        // Changing the password ends all other sessions: revoke every active refresh token.
        var now = clock.UtcNow;
        var active = await db.RefreshTokens
            .Where(t => t.UserId == cmd.UserId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in active)
        {
            token.Revoke(now);
        }

        audit.Add("password.changed", tenant.TenantId, cmd.UserId, null);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
