using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Application.Common;
using NexConvo.Identity.Domain.Roles;

namespace NexConvo.Identity.Application.Roles;

/// <summary>Renames a custom role and replaces its permissions. Authenticated (roles:manage).</summary>
public sealed record UpdateRoleCommand(Guid RoleId, string Name, IReadOnlyList<string> Permissions, Guid ActorUserId)
    : IRequest<Result>, IRequireVerifiedActor;

public sealed class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Permissions).NotEmpty().WithMessage("A role must grant at least one permission.");
        RuleForEach(x => x.Permissions)
            .Must(PermissionCatalog.IsAssignable)
            .WithMessage("Unknown or non-assignable permission.");
    }
}

public sealed class UpdateRoleCommandHandler(IIdentityDbContext db, ITenantContext tenant, IAuditWriter audit)
    : IRequestHandler<UpdateRoleCommand, Result>
{
    public async Task<Result> Handle(UpdateRoleCommand cmd, CancellationToken cancellationToken)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == cmd.RoleId, cancellationToken);
        if (role is null)
        {
            return Result.NotFound("Role not found.");
        }

        if (role.IsSystem)
        {
            return Result.Forbidden("System roles cannot be modified.");
        }

        var name = cmd.Name.Trim();
        if (await db.Roles.AnyAsync(r => r.Name == name && r.Id != cmd.RoleId, cancellationToken))
        {
            return Result.Conflict("A role with that name already exists.");
        }

        role.Rename(name);
        role.UpdatePermissions(cmd.Permissions);
        audit.Add("role.updated", tenant.TenantId, cmd.ActorUserId, $"role={role.Name}");
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
