using NexConvo.BuildingBlocks.Domain;

namespace NexConvo.Identity.Domain.Users;

/// <summary>Tenant-scoped join between a user and a role.</summary>
public sealed class UserRole : ITenantEntity
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }

    private UserRole() { } // EF

    public UserRole(Guid tenantId, Guid userId, Guid roleId)
    {
        TenantId = tenantId;
        UserId = userId;
        RoleId = roleId;
    }
}
