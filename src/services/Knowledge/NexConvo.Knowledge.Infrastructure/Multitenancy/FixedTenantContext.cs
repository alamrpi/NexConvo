using NexConvo.BuildingBlocks.Multitenancy;

namespace NexConvo.Knowledge.Infrastructure.Multitenancy;

/// <summary>
/// An ITenantContext pinned to one tenant, for background jobs that carry the tenant id in their
/// arguments instead of a JWT. Lets the RLS interceptor scope the connection exactly as it would
/// for an HTTP request — RLS stays enforced instead of being bypassed with an owner connection.
/// </summary>
internal sealed class FixedTenantContext(Guid tenantId) : ITenantContext
{
    public bool HasTenant => true;
    public Guid TenantId => tenantId;
}
