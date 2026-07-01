using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace NexConvo.Identity.IntegrationTests;

public sealed class UsersTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    private sealed record Tokens(string AccessToken, int ExpiresInSeconds, string RefreshToken);
    private sealed record RoleDto(Guid Id, string Name, bool IsSystem, bool GrantsAll, string[] Permissions, int MemberCount);
    private sealed record UserItem(
        Guid Id, string Email, string FullName, string Status, bool EmailVerified,
        Guid? RoleId, string? RoleName, DateTimeOffset CreatedAt);
    private sealed record PagedUsers(UserItem[] Items, int Total, int Page, int PageSize);
    private sealed record PendingInvite(Guid Id, string Email, string RoleName, DateTimeOffset ExpiresAt);

    private async Task<HttpClient> OwnerClient(string slug, string email)
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

    private static async Task<Guid> RoleId(HttpClient client, string name) =>
        (await client.GetFromJsonAsync<RoleDto[]>("/api/v1/roles"))!.Single(r => r.Name == name).Id;

    [Fact]
    public async Task ListUsers_ReturnsOwner_WithOwnerRole()
    {
        var client = await OwnerClient("users-list", "o@users-list.test");

        var page = (await client.GetFromJsonAsync<PagedUsers>("/api/v1/users"))!;

        page.Total.Should().Be(1);
        var owner = page.Items.Single();
        owner.RoleName.Should().Be("Owner");
        owner.Status.Should().Be("Active");
    }

    [Fact]
    public async Task ChangeOwnRole_AsOnlyOwner_IsBlocked()
    {
        var client = await OwnerClient("users-owner", "o@users-owner.test");
        var ownerId = (await client.GetFromJsonAsync<PagedUsers>("/api/v1/users"))!.Items.Single().Id;
        var memberRole = await RoleId(client, "Member");

        var resp = await client.PutAsJsonAsync($"/api/v1/users/{ownerId}/role", new { roleId = memberRole });

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict); // workspace must keep an Owner
    }

    [Fact]
    public async Task DeactivateSelf_IsBlocked()
    {
        var client = await OwnerClient("users-self", "o@users-self.test");
        var ownerId = (await client.GetFromJsonAsync<PagedUsers>("/api/v1/users"))!.Items.Single().Id;

        var resp = await client.PostAsync($"/api/v1/users/{ownerId}/deactivate", null);

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Invitation_Revoke_RemovesFromPending()
    {
        var client = await OwnerClient("users-rev", "o@users-rev.test");
        await client.PostAsJsonAsync("/api/v1/invitations", new { email = "x@users-rev.test", roleName = "Member" });
        var id = (await client.GetFromJsonAsync<PendingInvite[]>("/api/v1/invitations"))!.Single().Id;

        var del = await client.DeleteAsync($"/api/v1/invitations/{id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetFromJsonAsync<PendingInvite[]>("/api/v1/invitations"))!.Should().BeEmpty();
    }

    [Fact]
    public async Task Invitation_Resend_StaysPending()
    {
        var client = await OwnerClient("users-res", "o@users-res.test");
        await client.PostAsJsonAsync("/api/v1/invitations", new { email = "y@users-res.test", roleName = "Member" });
        var id = (await client.GetFromJsonAsync<PendingInvite[]>("/api/v1/invitations"))!.Single().Id;

        var resend = await client.PostAsync($"/api/v1/invitations/{id}/resend", null);
        resend.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetFromJsonAsync<PendingInvite[]>("/api/v1/invitations"))!.Should().ContainSingle();
    }

    [Fact]
    public async Task ChangeMemberRole_AndDeactivate_ControlsLogin()
    {
        var owner = await OwnerClient("users-mem", "o@users-mem.test");
        await owner.PostAsJsonAsync("/api/v1/invitations", new { email = "mate@users-mem.test", roleName = "Member" });

        var inviteToken = await ExtractToken("mate@users-mem.test", "/accept-invite");
        var accept = await factory.CreateClient().PostAsJsonAsync("/api/v1/invitations/accept",
            new { token = inviteToken, fullName = "Mate", password = "mate-pass-123" });
        accept.EnsureSuccessStatusCode();

        async Task<HttpStatusCode> LoginMate() =>
            (await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
                new { tenantSlug = "users-mem", email = "mate@users-mem.test", password = "mate-pass-123" })).StatusCode;

        (await LoginMate()).Should().Be(HttpStatusCode.OK);

        var mate = (await owner.GetFromJsonAsync<PagedUsers>("/api/v1/users"))!
            .Items.Single(u => u.Email == "mate@users-mem.test");

        var viewer = await RoleId(owner, "Viewer");
        (await owner.PutAsJsonAsync($"/api/v1/users/{mate.Id}/role", new { roleId = viewer }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await owner.PostAsync($"/api/v1/users/{mate.Id}/deactivate", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LoginMate()).Should().Be(HttpStatusCode.Forbidden); // disabled account can't log in

        (await owner.PostAsync($"/api/v1/users/{mate.Id}/reactivate", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LoginMate()).Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task Users_AreTenantIsolated()
    {
        await OwnerClient("users-iso-a", "o@users-iso-a.test");
        var tenantB = await OwnerClient("users-iso-b", "o@users-iso-b.test");

        var bUsers = (await tenantB.GetFromJsonAsync<PagedUsers>("/api/v1/users"))!;

        bUsers.Items.Should().OnlyContain(u => u.Email == "o@users-iso-b.test"); // RLS hides tenant A
    }

    private async Task<string> ExtractToken(string toEmail, string linkPath)
    {
        using var http = new HttpClient { BaseAddress = new Uri(factory.MailpitApiBaseUrl) };
        var pattern = new Regex(Regex.Escape(linkPath) + @"\?token=([^""&\s]+)", RegexOptions.IgnoreCase);

        for (var attempt = 0; attempt < 20; attempt++)
        {
            var search = await http.GetFromJsonAsync<JsonElement>(
                $"/api/v1/search?query=to%3A{Uri.EscapeDataString(toEmail)}");
            if (search.TryGetProperty("messages", out var messages) && messages.GetArrayLength() > 0)
            {
                var id = messages[0].GetProperty("ID").GetString();
                var message = await http.GetFromJsonAsync<JsonElement>($"/api/v1/message/{id}");
                var html = message.GetProperty("HTML").GetString() ?? string.Empty;
                var match = pattern.Match(html);
                if (match.Success)
                {
                    return Uri.UnescapeDataString(match.Groups[1].Value);
                }
            }

            await Task.Delay(300);
        }

        throw new InvalidOperationException($"No {linkPath} email captured for {toEmail}.");
    }
}
