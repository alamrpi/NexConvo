using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Application.Authentication;
using NexConvo.Identity.Application.Common;
using NexConvo.Identity.Domain.Users;
using DomainRefreshToken = NexConvo.Identity.Domain.Authentication.RefreshToken;

namespace NexConvo.Identity.Application.Invitations;

/// <summary>Accepts an invitation: provisions the user (pre-verified) and logs them in. Anonymous.</summary>
public sealed record AcceptInvitationCommand(string Token, string FullName, string Password)
    : IRequest<Result<AuthTokensDto>>;

public sealed class AcceptInvitationCommandValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
    }
}

public sealed class AcceptInvitationCommandHandler(
    IIdentityDbContext db,
    ILinkTokenService linkTokens,
    IPasswordHasher passwordHasher,
    IJwtTokenIssuer tokenIssuer,
    IRefreshTokenService refreshTokens,
    IAmbientTenantSetter tenantSetter,
    IAuditWriter audit,
    IClock clock) : IRequestHandler<AcceptInvitationCommand, Result<AuthTokensDto>>
{
    private static readonly Result<AuthTokensDto> Invalid =
        Result<AuthTokensDto>.Invalid("This invitation is invalid or has expired.");

    public async Task<Result<AuthTokensDto>> Handle(AcceptInvitationCommand cmd, CancellationToken cancellationToken)
    {
        if (!linkTokens.TryGetTenantId(cmd.Token, out var tenantId))
        {
            return Invalid;
        }

        tenantSetter.SetTenant(tenantId);

        var hash = linkTokens.Hash(cmd.Token);
        var invitation = await db.Invitations.FirstOrDefaultAsync(i => i.TokenHash == hash, cancellationToken);
        if (invitation is null || !invitation.IsPending(clock.UtcNow))
        {
            return Invalid;
        }

        if (await db.Users.AnyAsync(u => u.Email == invitation.Email, cancellationToken))
        {
            return Result<AuthTokensDto>.Conflict("You’re already a member — please sign in.");
        }

        var user = User.Register(tenantId, invitation.Email, passwordHasher.Hash(cmd.Password), cmd.FullName);
        user.AssignRole(invitation.RoleId);
        user.MarkEmailVerified(clock.UtcNow); // accepting via the emailed link proves email ownership
        db.Users.Add(user);
        invitation.Accept(clock.UtcNow);

        var generated = refreshTokens.Generate(tenantId);
        db.RefreshTokens.Add(DomainRefreshToken.Issue(tenantId, user.Id, generated.TokenHash, generated.ExpiresAt));

        audit.Add("invitation.accepted", tenantId, user.Id, null);
        await db.SaveChangesAsync(cancellationToken);

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == invitation.RoleId, cancellationToken);
        var (roleNames, permissions) = RoleProjection.From(role is null ? [] : [role]);
        var access = tokenIssuer.Issue(user, tenant!, roleNames, permissions);
        return Result.Success(new AuthTokensDto(access.Token, access.ExpiresInSeconds, generated.Token));
    }
}
