namespace NexConvo.Identity.Domain.Roles;

/// <summary>
/// Where a permission sits in a module's escalation ladder. Drives the role editor's quick-set
/// presets: Read-only = Access, Standard = Access+Manage, Full = Access+Manage+Operations.
/// </summary>
public enum PermissionCategory
{
    Access = 0,
    Manage = 1,
    Operations = 2,
}

/// <summary>A permission that can be granted to a custom role, with its module + category grouping.</summary>
public sealed record PermissionDescriptor(string Key, string Module, PermissionCategory Category);

/// <summary>
/// The single source of truth for which permissions a custom role may grant, grouped by module
/// and category (matching the role-editor UI). The Owner-only wildcard (<see cref="Permissions.All"/>)
/// is deliberately excluded — it can never be assigned to a custom role. Only permissions for
/// shipped features live here; it grows as each module lands. The frontend localizes the keys.
/// </summary>
public static class PermissionCatalog
{
    public static readonly IReadOnlyList<PermissionDescriptor> Assignable =
    [
        // Users module
        new(Permissions.UsersRead, "users", PermissionCategory.Access),
        new(Permissions.UsersManage, "users", PermissionCategory.Manage),
        new(Permissions.UsersInvite, "users", PermissionCategory.Operations),

        // Roles module
        new(Permissions.RolesManage, "roles", PermissionCategory.Manage),

        // Workspace / settings module
        new(Permissions.SettingsManage, "workspace", PermissionCategory.Manage),
        new(Permissions.TenantManage, "workspace", PermissionCategory.Operations),

        // Leads module (placeholder feature, real keys)
        new(Permissions.LeadsRead, "leads", PermissionCategory.Access),
        new(Permissions.LeadsWrite, "leads", PermissionCategory.Manage),
    ];

    private static readonly HashSet<string> AssignableKeys =
        Assignable.Select(p => p.Key).ToHashSet(StringComparer.Ordinal);

    public static bool IsAssignable(string key) => AssignableKeys.Contains(key);
}
