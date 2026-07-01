using Microsoft.AspNetCore.Http;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.Identity.Application.Abstractions;

namespace NexConvo.Identity.Infrastructure.Multitenancy;

/// <summary>
/// Tenant context for the Identity service. Returns an explicitly-set tenant (used by auth
/// handlers before a JWT exists — signup/login/refresh) and otherwise falls back to the JWT
/// <c>tenant_id</c> claim (used by authenticated endpoints like /me). Scoped per request.
/// </summary>
public sealed class AmbientTenantContext(IHttpContextAccessor accessor) : ITenantContext, IAmbientTenantSetter
{
    private Guid? _explicitTenantId;

    public void SetTenant(Guid tenantId) => _explicitTenantId = tenantId;

    public bool HasTenant => _explicitTenantId is not null || TryGetJwtTenant(out _);

    public Guid TenantId =>
        _explicitTenantId
        ?? (TryGetJwtTenant(out var id) ? id : throw new InvalidOperationException("No tenant in scope."));

    private bool TryGetJwtTenant(out Guid tenantId)
    {
        var raw = accessor.HttpContext?.User.FindFirst(HttpTenantContext.TenantClaimType)?.Value;
        return Guid.TryParse(raw, out tenantId);
    }
}
