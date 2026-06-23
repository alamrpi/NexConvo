using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Domain.Authentication;

namespace NexConvo.Identity.Application.Authentication.EmailVerification;

/// <summary>Consumes an email-verification link token and marks the user verified. Anonymous.</summary>
public sealed record VerifyEmailCommand(string Token) : IRequest<Result>;

public sealed class VerifyEmailCommandValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailCommandValidator() => RuleFor(x => x.Token).NotEmpty();
}

public sealed class VerifyEmailCommandHandler(
    IIdentityDbContext db,
    ILinkTokenService linkTokens,
    IAmbientTenantSetter tenantSetter,
    IAuditWriter audit,
    IClock clock) : IRequestHandler<VerifyEmailCommand, Result>
{
    private static readonly Result Invalid = Result.Invalid("This verification link is invalid or has expired.");

    public async Task<Result> Handle(VerifyEmailCommand cmd, CancellationToken cancellationToken)
    {
        if (!linkTokens.TryGetTenantId(cmd.Token, out var tenantId))
        {
            return Invalid;
        }

        tenantSetter.SetTenant(tenantId);

        var hash = linkTokens.Hash(cmd.Token);
        var token = await db.OneTimeTokens.FirstOrDefaultAsync(
            t => t.TokenHash == hash && t.Purpose == OneTimeTokenPurpose.EmailVerification, cancellationToken);
        if (token is null || !token.IsValid(clock.UtcNow))
        {
            return Invalid;
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == token.UserId, cancellationToken);
        if (user is null)
        {
            return Invalid;
        }

        user.MarkEmailVerified(clock.UtcNow);
        token.Consume(clock.UtcNow);
        audit.Add("email.verified", tenantId, user.Id, null);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
