using NexConvo.BuildingBlocks.Domain;
using NexConvo.Identity.Domain.Events;
using NexConvo.Identity.Domain.ValueObjects;

namespace NexConvo.Identity.Domain.Tenants;

public enum TenantStatus
{
    Active = 0,
    Suspended = 1,
}

/// <summary>
/// The tenant registry root. NOT tenant-scoped (no RLS) — it is the lookup table that login
/// resolves by <see cref="Slug"/> before any tenant context exists.
/// </summary>
public sealed class Tenant : BaseEntity
{
    public string Name { get; private set; } = null!;
    public TenantSlug Slug { get; private set; } = null!;
    public TenantStatus Status { get; private set; }

    /// <summary>When true, members must have 2FA enabled to perform sensitive writes (soft gate).</summary>
    public bool RequireTwoFactor { get; private set; }

    private Tenant() { } // EF

    private Tenant(string name, TenantSlug slug)
    {
        Name = name;
        Slug = slug;
        Status = TenantStatus.Active;
    }

    public static Tenant Provision(string name, TenantSlug slug)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Tenant name is required.");
        }

        var tenant = new Tenant(name.Trim(), slug);
        tenant.RaiseDomainEvent(new TenantProvisionedDomainEvent(tenant.Id, tenant.Name, slug.Value));
        return tenant;
    }

    /// <summary>Toggle the workspace-wide 2FA requirement (Owner/settings:manage policy).</summary>
    public void SetRequireTwoFactor(bool required) => RequireTwoFactor = required;
}
