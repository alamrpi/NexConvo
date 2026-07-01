using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NexConvo.Identity.Infrastructure.Security;

namespace NexConvo.Identity.IntegrationTests;

public sealed class WorkspaceTwoFactorEnforcementTests(IdentityApiFactory factory)
    : IClassFixture<IdentityApiFactory>
{
    private sealed record Tokens(string AccessToken, int ExpiresInSeconds, string RefreshToken);
    private sealed record Enrollment(string Secret, string OtpAuthUri);
    private sealed record BackupCodesResponse(string[] BackupCodes);
    private sealed record Security(bool RequireTwoFactor);
    private sealed record Me(
        Guid UserId, Guid TenantId, string TenantSlug, string Email, string FullName,
        bool EmailVerified, bool TwoFactorEnabled, bool WorkspaceRequiresTwoFactor,
        string[] Roles, string[] Permissions);

    private static string CurrentCode(string secretBase32) =>
        TotpService.ComputeCode(Base32.Decode(secretBase32), DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);

    private async Task<HttpClient> SignupOwner(string slug, string email)
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

    private static async Task Enroll(HttpClient client)
    {
        var start = await client.PostAsync("/api/v1/account/2fa/start", null);
        start.EnsureSuccessStatusCode();
        var enrollment = (await start.Content.ReadFromJsonAsync<Enrollment>())!;
        var confirm = await client.PostAsJsonAsync("/api/v1/account/2fa/confirm", new { code = CurrentCode(enrollment.Secret) });
        confirm.StatusCode.Should().Be(HttpStatusCode.OK);
        (await confirm.Content.ReadFromJsonAsync<BackupCodesResponse>())!.BackupCodes.Should().HaveCount(10);
    }

    [Fact]
    public async Task SecuritySettings_DefaultFalse_PersistsToggle_AndReflectedInMe()
    {
        var client = await SignupOwner("wtfa-get", "o@wtfa-get.test");

        (await client.GetFromJsonAsync<Security>("/api/v1/settings/security"))!.RequireTwoFactor.Should().BeFalse();
        (await client.GetFromJsonAsync<Me>("/api/v1/auth/me"))!.WorkspaceRequiresTwoFactor.Should().BeFalse();

        var put = await client.PutAsJsonAsync("/api/v1/settings/security", new { requireTwoFactor = true });
        put.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetFromJsonAsync<Security>("/api/v1/settings/security"))!.RequireTwoFactor.Should().BeTrue();
        (await client.GetFromJsonAsync<Me>("/api/v1/auth/me"))!.WorkspaceRequiresTwoFactor.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task RequiringTwoFactor_BlocksSensitiveWrites_UntilActorEnrolls()
    {
        var client = await SignupOwner("wtfa-gate", "o@wtfa-gate.test");

        // Baseline: requirement off + not enrolled → a sensitive write succeeds.
        var before = await client.PostAsJsonAsync("/api/v1/invitations", new { email = "a@wtfa-gate.test", roleName = "Member" });
        before.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The (un-enrolled) Owner can still turn the requirement ON — no precondition.
        var toggle = await client.PutAsJsonAsync("/api/v1/settings/security", new { requireTwoFactor = true });
        toggle.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Now the same write is blocked because the actor has no 2FA.
        var blocked = await client.PostAsJsonAsync("/api/v1/invitations", new { email = "b@wtfa-gate.test", roleName = "Member" });
        blocked.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // After enrolling 2FA, the write is allowed again (behavior checks live DB).
        await Enroll(client);
        var allowed = await client.PostAsJsonAsync("/api/v1/invitations", new { email = "c@wtfa-gate.test", roleName = "Member" });
        allowed.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
