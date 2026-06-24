using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;

namespace NexConvo.Identity.Application.Account;

/// <summary>Updates the current user's own profile. Authenticated (self).</summary>
public sealed record UpdateProfileCommand(Guid UserId, string FullName) : IRequest<Result>;

public sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator() =>
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
}

public sealed class UpdateProfileCommandHandler(
    IIdentityDbContext db, ITenantContext tenant, IAuditWriter audit, ICurrentUserCache cache)
    : IRequestHandler<UpdateProfileCommand, Result>
{
    public async Task<Result> Handle(UpdateProfileCommand cmd, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == cmd.UserId, cancellationToken);
        if (user is null)
        {
            return Result.NotFound("User not found.");
        }

        user.UpdateProfile(cmd.FullName);
        audit.Add("profile.updated", tenant.TenantId, cmd.UserId, null);
        await db.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(cmd.UserId, cancellationToken);

        return Result.Success();
    }
}
