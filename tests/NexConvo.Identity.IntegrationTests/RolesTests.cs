using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace NexConvo.Identity.IntegrationTests;

public sealed class RolesTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    private sealed record Tokens(string AccessToken, int ExpiresInSeconds, string RefreshToken);
    private sealed record RoleDto(
        Guid Id, string Name, bool IsSystem, bool GrantsAll, string[] Permissions, int MemberCount);
    private sealed record PermissionItemDto(string Key, string Module, string Category);

    private async Task<HttpClient> AuthedOwnerClient(string slug, string email)
    {
        var client = factory.CreateClient();
        var signup = await client.PostAsJsonAsync("/api/v1/auth/signup", new
        {
            tenantName = "Acme", tenantSlug = slug, email, password = "password123", fullName = "Owner",
        });
        signup.EnsureSuccessStatusCode();
        var tokens = await signup.Content.ReadFromJsonAsync<Tokens>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens!.AccessToken);
        return client;
    }

    [Fact]
    public async Task Signup_Seeds_OwnerAdminSystem_PlusMemberViewerStarters()
    {
        var client = await AuthedOwnerClient("roles-seed", "o@roles-seed.test");

        var roles = (await client.GetFromJsonAsync<RoleDto[]>("/api/v1/roles"))!;

        roles.Select(r => r.Name).Should().BeEquivalentTo("Owner", "Admin", "Member", "Viewer");
        roles.Single(r => r.Name == "Owner").GrantsAll.Should().BeTrue();
        roles.Single(r => r.Name == "Admin").IsSystem.Should().BeTrue();
        roles.Single(r => r.Name == "Member").IsSystem.Should().BeFalse();
    }

    [Fact]
    public async Task PermissionCatalog_ReturnsAssignableKeys_GroupedByModule_WithoutWildcard()
    {
        var client = await AuthedOwnerClient("roles-cat", "o@roles-cat.test");

        var items = await client.GetFromJsonAsync<PermissionItemDto[]>("/api/v1/roles/permissions");

        items.Should().NotBeNullOrEmpty();
        items!.Should().Contain(i => i.Key == "users:read" && i.Module == "users" && i.Category == "Access");
        items.Should().NotContain(i => i.Key == "*");
    }

    [Fact]
    public async Task CreateRole_ThenList_ShowsCustomRole_WithNoMembers()
    {
        var client = await AuthedOwnerClient("roles-crud", "o@roles-crud.test");

        var create = await client.PostAsJsonAsync("/api/v1/roles",
            new { name = "Support", permissions = new[] { "users:read", "leads:read" } });
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = await create.Content.ReadFromJsonAsync<Guid>();

        var roles = await client.GetFromJsonAsync<RoleDto[]>("/api/v1/roles");
        var support = roles!.Single(r => r.Id == id);
        support.IsSystem.Should().BeFalse();
        support.MemberCount.Should().Be(0);
        support.Permissions.Should().BeEquivalentTo("users:read", "leads:read");
    }

    [Fact]
    public async Task UpdateRole_RenamesAndReplacesPermissions()
    {
        var client = await AuthedOwnerClient("roles-upd", "o@roles-upd.test");
        var create = await client.PostAsJsonAsync("/api/v1/roles",
            new { name = "Temp", permissions = new[] { "users:read" } });
        var id = await create.Content.ReadFromJsonAsync<Guid>();

        var update = await client.PutAsJsonAsync($"/api/v1/roles/{id}",
            new { name = "Renamed", permissions = new[] { "users:read", "users:manage" } });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var roles = await client.GetFromJsonAsync<RoleDto[]>("/api/v1/roles");
        var role = roles!.Single(r => r.Id == id);
        role.Name.Should().Be("Renamed");
        role.Permissions.Should().BeEquivalentTo("users:read", "users:manage");
    }

    [Fact]
    public async Task CreateRole_WithWildcard_IsRejected()
    {
        var client = await AuthedOwnerClient("roles-star", "o@roles-star.test");

        var create = await client.PostAsJsonAsync("/api/v1/roles",
            new { name = "Sneaky", permissions = new[] { "*" } });

        create.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task UpdateOrDelete_SystemRole_IsForbidden()
    {
        var client = await AuthedOwnerClient("roles-sys", "o@roles-sys.test");
        var roles = await client.GetFromJsonAsync<RoleDto[]>("/api/v1/roles");
        var admin = roles!.Single(r => r.Name == "Admin");

        var update = await client.PutAsJsonAsync($"/api/v1/roles/{admin.Id}",
            new { name = "Admin2", permissions = new[] { "users:read" } });
        update.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var delete = await client.DeleteAsync($"/api/v1/roles/{admin.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteRole_RemovesCustomRole()
    {
        var client = await AuthedOwnerClient("roles-del", "o@roles-del.test");
        var create = await client.PostAsJsonAsync("/api/v1/roles",
            new { name = "Disposable", permissions = new[] { "users:read" } });
        var id = await create.Content.ReadFromJsonAsync<Guid>();

        var delete = await client.DeleteAsync($"/api/v1/roles/{id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var roles = await client.GetFromJsonAsync<RoleDto[]>("/api/v1/roles");
        roles!.Should().NotContain(r => r.Id == id);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task CustomRoles_AreTenantIsolated()
    {
        var tenantA = await AuthedOwnerClient("roles-iso-a", "o@roles-iso-a.test");
        await tenantA.PostAsJsonAsync("/api/v1/roles",
            new { name = "A-Only", permissions = new[] { "users:read" } });

        var tenantB = await AuthedOwnerClient("roles-iso-b", "o@roles-iso-b.test");
        var rolesB = await tenantB.GetFromJsonAsync<RoleDto[]>("/api/v1/roles");

        rolesB!.Should().NotContain(r => r.Name == "A-Only"); // RLS keeps tenant A's role invisible
    }
}
