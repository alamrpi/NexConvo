using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Application.Common;
using NexConvo.Identity.Domain.Roles;

namespace NexConvo.Identity.Application.Roles;

/// <summary>Creates a tenant custom role. Authenticated (roles:manage).</summary>
public sealed record CreateRoleCommand(string Name, IReadOnlyList<string> Permissions, Guid ActorUserId)
    : IRequest<Result<Guid>>, IRequireVerifiedActor;

public sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Permissions).NotEmpty().WithMessage("A role must grant at least one permission.");
        RuleForEach(x => x.Permissions)
            .Must(PermissionCatalog.IsAssignable)
            .WithMessage("Unknown or non-assignable permission.");
    }
}

public sealed class CreateRoleCommandHandler(IIdentityDbContext db, ITenantContext tenant, IAuditWriter audit)
    : IRequestHandler<CreateRoleCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateRoleCommand cmd, CancellationToken cancellationToken)
    {
        var name = cmd.Name.Trim();
        if (await db.Roles.AnyAsync(r => r.Name == name, cancellationToken))
        {
            return Result<Guid>.Conflict("A role with that name already exists.");
        }

        var role = Role.CreateCustom(tenant.TenantId, name, cmd.Permissions);
        db.Roles.Add(role);
        audit.Add("role.created", tenant.TenantId, cmd.ActorUserId, $"role={role.Name}");
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(role.Id);
    }
}
