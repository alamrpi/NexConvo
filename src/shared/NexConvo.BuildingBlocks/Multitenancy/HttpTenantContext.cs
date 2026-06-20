using Microsoft.AspNetCore.Http;

namespace NexConvo.BuildingBlocks.Multitenancy;

/// <summary>Reads the tenant from the authenticated principal's <c>tenant_id</c> claim.</summary>
public sealed class HttpTenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    public const string TenantClaimType = "tenant_id";

    public bool HasTenant => TryGetTenant(out _);

    public Guid TenantId =>
        TryGetTenant(out var tenantId)
            ? tenantId
            : throw new InvalidOperationException("No 'tenant_id' claim on the current principal.");

    private bool TryGetTenant(out Guid tenantId)
    {
        var raw = accessor.HttpContext?.User.FindFirst(TenantClaimType)?.Value;
        return Guid.TryParse(raw, out tenantId);
    }
}
