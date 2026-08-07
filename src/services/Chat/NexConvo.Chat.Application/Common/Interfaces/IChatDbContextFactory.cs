namespace NexConvo.Chat.Application.Common.Interfaces;

/// <summary>
/// Builds an <see cref="IChatDbContext"/> pinned to a specific tenant, for code paths that have no
/// ambient HTTP request to source a tenant from (MassTransit consumers, background jobs). The
/// DI-scoped IChatDbContext relies on HttpTenantContext, which has no tenant outside a request —
/// using it there would open a tenant-less connection and RLS would silently hide every row.
/// </summary>
public interface IChatDbContextFactory
{
    IChatDbContext CreateForTenant(Guid tenantId);
}
