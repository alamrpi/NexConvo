namespace NexConvo.Identity.Domain.Roles;

/// <summary>
/// Canonical permission keys. Services map their endpoint policies to these. "*" is the
/// wildcard granted to the tenant Owner. Kept small for the first cut; extended per module.
/// </summary>
public static class Permissions
{
    public const string All = "*";

    public const string TenantManage = "tenant:manage";
    public const string UsersRead = "users:read";
    public const string UsersInvite = "users:invite";
    public const string LeadsRead = "leads:read";
    public const string LeadsWrite = "leads:write";
}
