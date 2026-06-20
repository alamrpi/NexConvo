using NexConvo.Identity.Domain.Roles;

namespace NexConvo.Identity.Application.Authentication;

/// <summary>Projects a user's roles into the role-name + permission lists embedded in the JWT.</summary>
public static class RoleProjection
{
    public static (IReadOnlyCollection<string> Roles, IReadOnlyCollection<string> Permissions) From(
        IReadOnlyCollection<Role> roles)
    {
        var roleNames = roles.Select(r => r.Name).ToArray();

        var permissions = roles.Any(r => r.GrantsAll)
            ? new[] { Permissions.All }
            : roles.SelectMany(r => r.PermissionKeys).Distinct().ToArray();

        return (roleNames, permissions);
    }
}
