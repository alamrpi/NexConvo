using NexConvo.BuildingBlocks.Domain;

namespace NexConvo.Identity.Domain.Roles;

/// <summary>A tenant-scoped role carrying a set of permission keys (stored as JSONB).</summary>
public sealed class Role : BaseAggregateRoot
{
    private readonly List<string> _permissions = [];

    public string Name { get; private set; } = null!;
    public IReadOnlyCollection<string> PermissionKeys => _permissions.AsReadOnly();

    private Role() { } // EF

    private Role(Guid tenantId, string name, IEnumerable<string> permissions)
    {
        TenantId = tenantId;
        Name = name;
        _permissions.AddRange(permissions);
    }

    public bool GrantsAll => _permissions.Contains(Permissions.All);

    /// <summary>The three roles seeded for a freshly provisioned tenant.</summary>
    public static IReadOnlyList<Role> DefaultsFor(Guid tenantId) =>
    [
        new Role(tenantId, "Owner", [Permissions.All]),
        new Role(tenantId, "Admin",
        [
            Permissions.TenantManage, Permissions.SettingsManage, Permissions.UsersRead,
            Permissions.UsersInvite, Permissions.LeadsRead, Permissions.LeadsWrite,
        ]),
        new Role(tenantId, "Agent",
        [
            Permissions.UsersRead, Permissions.LeadsRead, Permissions.LeadsWrite,
        ]),
    ];
}
