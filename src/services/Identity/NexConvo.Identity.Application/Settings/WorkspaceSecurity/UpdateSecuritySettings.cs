using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;

namespace NexConvo.Identity.Application.Settings.WorkspaceSecurity;

/// <summary>
/// Toggles the workspace-wide 2FA requirement. Gated settings:manage (RBAC only). Deliberately
/// does NOT implement IRequireVerifiedActor — otherwise enabling the requirement would self-gate
/// when the acting Owner isn't enrolled (the agreed "no precondition" rule).
/// </summary>
public sealed record UpdateSecuritySettingsCommand(bool RequireTwoFactor, Guid ActorUserId) : IRequest<Result>;

public sealed class UpdateSecuritySettingsCommandHandler(
    IIdentityDbContext db, ITenantContext tenant, IAuditWriter audit)
    : IRequestHandler<UpdateSecuritySettingsCommand, Result>
{
    public async Task<Result> Handle(UpdateSecuritySettingsCommand cmd, CancellationToken cancellationToken)
    {
        var current = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenant.TenantId, cancellationToken);
        if (current is null)
        {
            return Result.NotFound();
        }

        current.SetRequireTwoFactor(cmd.RequireTwoFactor);
        audit.Add("workspace.2fa_required_changed", tenant.TenantId, cmd.ActorUserId, $"required={cmd.RequireTwoFactor}");
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
