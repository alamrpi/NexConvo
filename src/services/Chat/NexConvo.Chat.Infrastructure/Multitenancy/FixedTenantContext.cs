using NexConvo.BuildingBlocks.Multitenancy;

namespace NexConvo.Chat.Infrastructure.Multitenancy;

/// <summary>
/// An ITenantContext pinned to one tenant, for code paths with no ambient HTTP request (MassTransit
/// consumers, background jobs) that already know their tenant from the message/job payload. RLS
/// stays enforced, scoped to exactly that tenant — mirrors Knowledge's FixedTenantContext.
/// </summary>
public sealed class FixedTenantContext(Guid tenantId) : ITenantContext
{
    public bool HasTenant => true;
    public Guid TenantId => tenantId;
}
