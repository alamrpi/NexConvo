using NexConvo.BuildingBlocks.Domain;

namespace NexConvo.Identity.Domain.Roles;

/// <summary>A tenant-scoped role carrying a set of permission keys (stored as JSONB).</summary>
public sealed class Role : BaseAggregateRoot
{
    private readonly List<string> _permissions = [];

    public string Name { get; private set; } = null!;

    /// <summary>System roles (Owner, Admin) are seeded per tenant and cannot be edited or deleted.</summary>
    public bool IsSystem { get; private set; }

    public IReadOnlyCollection<string> PermissionKeys => _permissions.AsReadOnly();

    private Role() { } // EF

    private Role(Guid tenantId, string name, IEnumerable<string> permissions, bool isSystem)
    {
        TenantId = tenantId;
        Name = name;
        IsSystem = isSystem;
        _permissions.AddRange(permissions);
    }

    public bool GrantsAll => _permissions.Contains(Permissions.All);

    /// <summary>
    /// Roles seeded for a freshly provisioned tenant. Owner + Admin are immutable system roles;
    /// Member + Viewer are editable starter roles (a tenant can tweak or delete them).
    /// </summary>
    public static IReadOnlyList<Role> DefaultsFor(Guid tenantId) =>
    [
        new Role(tenantId, "Owner", [Permissions.All], isSystem: true),
        new Role(tenantId, "Admin",
        [
            Permissions.SettingsManage, Permissions.UsersRead, Permissions.UsersInvite,
            Permissions.UsersManage, Permissions.LeadsRead, Permissions.LeadsWrite,
        ], isSystem: true),
        new Role(tenantId, "Member",
        [
            Permissions.UsersRead, Permissions.LeadsRead, Permissions.LeadsWrite,
        ], isSystem: false),
        new Role(tenantId, "Viewer",
        [
            Permissions.UsersRead, Permissions.LeadsRead,
        ], isSystem: false),
    ];

    /// <summary>Creates a tenant-defined custom role. The Owner-only wildcard cannot be granted here.</summary>
    public static Role CreateCustom(Guid tenantId, string name, IEnumerable<string> permissionKeys) =>
        new(tenantId, RequireName(name), NormalizePermissions(permissionKeys), isSystem: false);

    public void Rename(string name)
    {
        EnsureEditable();
        Name = RequireName(name);
    }

    public void UpdatePermissions(IEnumerable<string> permissionKeys)
    {
        EnsureEditable();
        var keys = NormalizePermissions(permissionKeys);
        _permissions.Clear();
        _permissions.AddRange(keys);
    }

    private void EnsureEditable()
    {
        if (IsSystem)
        {
            throw new DomainException("System roles cannot be modified.");
        }
    }

    private static string RequireName(string name) =>
        string.IsNullOrWhiteSpace(name) ? throw new DomainException("Role name is required.") : name.Trim();

    private static List<string> NormalizePermissions(IEnumerable<string> permissionKeys)
    {
        var keys = permissionKeys.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct(StringComparer.Ordinal).ToList();
        if (keys.Contains(Permissions.All))
        {
            throw new DomainException("The wildcard permission cannot be assigned to a custom role.");
        }

        if (keys.Count == 0)
        {
            throw new DomainException("A role must grant at least one permission.");
        }

        return keys;
    }
}
