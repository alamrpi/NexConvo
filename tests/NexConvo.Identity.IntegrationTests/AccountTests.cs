using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace NexConvo.Identity.IntegrationTests;

public sealed class AccountTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    private sealed record Tokens(string AccessToken, int ExpiresInSeconds, string RefreshToken);
    private sealed record Me(
        Guid UserId, Guid TenantId, string TenantSlug, string Email, string FullName,
        bool EmailVerified, bool TwoFactorEnabled, string[] Roles, string[] Permissions);

    private async Task<(HttpClient Client, Tokens Tokens)> SignupOwner(string slug, string email)
    {
        var client = factory.CreateClient();
        var signup = await client.PostAsJsonAsync("/api/v1/auth/signup", new
        {
            tenantName = "Acme", tenantSlug = slug, email, password = "password123", fullName = "Owner",
        });
        signup.EnsureSuccessStatusCode();
        var tokens = (await signup.Content.ReadFromJsonAsync<Tokens>())!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        return (client, tokens);
    }

    [Fact]
    public async Task Me_IncludesEmailVerified_FalseForFreshSignup()
    {
        var (client, _) = await SignupOwner("acct-me", "o@acct-me.test");
        var me = await client.GetFromJsonAsync<Me>("/api/v1/auth/me");
        me!.EmailVerified.Should().BeFalse();
        me.TwoFactorEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateProfile_ChangesName_ReflectedInMe()
    {
        var (client, _) = await SignupOwner("acct-prof", "o@acct-prof.test");

        var resp = await client.PutAsJsonAsync("/api/v1/account/profile", new { fullName = "New Name" });
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var me = await client.GetFromJsonAsync<Me>("/api/v1/auth/me");
        me!.FullName.Should().Be("New Name");
    }

    [Fact]
    public async Task ChangePassword_WrongCurrent_Returns401()
    {
        var (client, _) = await SignupOwner("acct-pw1", "o@acct-pw1.test");

        var resp = await client.PutAsJsonAsync("/api/v1/account/password",
            new { currentPassword = "wrong-pass", newPassword = "brand-new-123" });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_Succeeds_RotatesSessions_AndNewPasswordWorks()
    {
        var (client, tokens) = await SignupOwner("acct-pw2", "o@acct-pw2.test");

        var change = await client.PutAsJsonAsync("/api/v1/account/password",
            new { currentPassword = "password123", newPassword = "brand-new-123" });
        change.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The old refresh token is revoked (other sessions ended).
        var oldRefresh = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = tokens.RefreshToken });
        oldRefresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var newLogin = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { tenantSlug = "acct-pw2", email = "o@acct-pw2.test", password = "brand-new-123" });
        newLogin.StatusCode.Should().Be(HttpStatusCode.OK);

        var oldLogin = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { tenantSlug = "acct-pw2", email = "o@acct-pw2.test", password = "password123" });
        oldLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task UnverifiedUser_IsBlockedFromSensitiveWrites_UntilVerified()
    {
        // Re-enable the soft-verification guard for this host only (base factory disables it).
        using var strict = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:RequireVerifiedEmailForWrites"] = "true",
            })));
        var client = strict.CreateClient();

        var signup = await client.PostAsJsonAsync("/api/v1/auth/signup", new
        {
            tenantName = "Acme", tenantSlug = "acct-verify", email = "o@acct-verify.test",
            password = "password123", fullName = "Owner",
        });
        var tokens = (await signup.Content.ReadFromJsonAsync<Tokens>())!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);

        // Unverified → a sensitive write (invite) is blocked.
        var blocked = await client.PostAsJsonAsync("/api/v1/invitations",
            new { email = "mate@acct-verify.test", roleName = "Member" });
        blocked.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Verify via the emailed token… (the strict host shares the base factory's Mailpit)
        var token = await ExtractToken("o@acct-verify.test", "/verify-email");
        var verify = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new { token });
        verify.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // …and the same write now succeeds (guard checks live DB, no token refresh needed).
        var allowed = await client.PostAsJsonAsync("/api/v1/invitations",
            new { email = "mate@acct-verify.test", roleName = "Member" });
        allowed.StatusCode.Should().Be(HttpStatusCode.NoContent);
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
