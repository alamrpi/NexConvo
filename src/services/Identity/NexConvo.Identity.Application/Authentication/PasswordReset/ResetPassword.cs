using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Domain.Authentication;

namespace NexConvo.Identity.Application.Authentication.PasswordReset;

/// <summary>Consumes a reset link token, sets the new password, and revokes existing refresh tokens.</summary>
public sealed record ResetPasswordCommand(string Token, string NewPassword) : IRequest<Result>;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(128);
    }
}

public sealed class ResetPasswordCommandHandler(
    IIdentityDbContext db,
    ILinkTokenService linkTokens,
    IPasswordHasher passwordHasher,
    IAmbientTenantSetter tenantSetter,
    IAuditWriter audit,
    IClock clock) : IRequestHandler<ResetPasswordCommand, Result>
{
    private static readonly Result Invalid = Result.Invalid("This reset link is invalid or has expired.");

    public async Task<Result> Handle(ResetPasswordCommand cmd, CancellationToken cancellationToken)
    {
        if (!linkTokens.TryGetTenantId(cmd.Token, out var tenantId))
        {
            return Invalid;
        }

        tenantSetter.SetTenant(tenantId);

        var hash = linkTokens.Hash(cmd.Token);
        var token = await db.OneTimeTokens.FirstOrDefaultAsync(
            t => t.TokenHash == hash && t.Purpose == OneTimeTokenPurpose.PasswordReset, cancellationToken);
        if (token is null || !token.IsValid(clock.UtcNow))
        {
            return Invalid;
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == token.UserId, cancellationToken);
        if (user is null)
        {
            return Invalid;
        }

        user.SetPasswordHash(passwordHasher.Hash(cmd.NewPassword));
        token.Consume(clock.UtcNow);

        // Invalidate every existing session — a reset should log out other devices.
        var activeTokens = await db.RefreshTokens
            .Where(r => r.UserId == user.Id && r.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var refreshToken in activeTokens)
        {
            refreshToken.Revoke(clock.UtcNow);
        }

        audit.Add("password.reset", tenantId, user.Id, null);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
