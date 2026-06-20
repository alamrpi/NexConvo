namespace NexConvo.BuildingBlocks.Multitenancy;

/// <summary>
/// The current tenant, resolved from the JWT <c>tenant_id</c> claim (skill Standard 6).
/// Never populated from a request body.
/// </summary>
public interface ITenantContext
{
    /// <summary>True when a tenant is in scope (false for non-request contexts like migrations).</summary>
    bool HasTenant { get; }

    /// <summary>The current tenant id. Throws when no tenant is in scope.</summary>
    Guid TenantId { get; }
}
