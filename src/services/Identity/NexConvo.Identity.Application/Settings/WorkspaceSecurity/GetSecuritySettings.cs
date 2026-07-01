using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;

namespace NexConvo.Identity.Application.Settings.WorkspaceSecurity;

public sealed record SecuritySettingsDto(bool RequireTwoFactor);

/// <summary>The workspace's security policy (currently just the 2FA requirement). Gated settings:manage.</summary>
public sealed record GetSecuritySettingsQuery : IRequest<Result<SecuritySettingsDto>>;

public sealed class GetSecuritySettingsQueryHandler(IIdentityDbContext db, ITenantContext tenant)
    : IRequestHandler<GetSecuritySettingsQuery, Result<SecuritySettingsDto>>
{
    public async Task<Result<SecuritySettingsDto>> Handle(
        GetSecuritySettingsQuery query, CancellationToken cancellationToken)
    {
        var current = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenant.TenantId, cancellationToken);
        return current is null
            ? Result<SecuritySettingsDto>.NotFound()
            : Result.Success(new SecuritySettingsDto(current.RequireTwoFactor));
    }
}
