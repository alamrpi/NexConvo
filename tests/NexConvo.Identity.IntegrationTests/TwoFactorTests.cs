using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NexConvo.Identity.Infrastructure.Security;

namespace NexConvo.Identity.IntegrationTests;

public sealed class TwoFactorTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    private sealed record Tokens(string AccessToken, int ExpiresInSeconds, string RefreshToken);
    private sealed record Enrollment(string Secret, string OtpAuthUri);
    private sealed record BackupCodesResponse(string[] BackupCodes);
    private sealed record LoginChallenge(bool TwoFactorRequired, string ChallengeToken);

    /// <summary>Computes the current valid TOTP code for a secret (mirrors what an authenticator app does).</summary>
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

    private static async Task<(string Secret, string[] BackupCodes)> Enroll(HttpClient client)
    {
        var start = await client.PostAsync("/api/v1/account/2fa/start", null);
        start.EnsureSuccessStatusCode();
        var enrollment = (await start.Content.ReadFromJsonAsync<Enrollment>())!;

        var confirm = await client.PostAsJsonAsync("/api/v1/account/2fa/confirm", new { code = CurrentCode(enrollment.Secret) });
        confirm.StatusCode.Should().Be(HttpStatusCode.OK);
        var codes = (await confirm.Content.ReadFromJsonAsync<BackupCodesResponse>())!;
        codes.BackupCodes.Should().HaveCount(10);
        return (enrollment.Secret, codes.BackupCodes);
    }

    private async Task<LoginChallenge> Login(string slug, string email)
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { tenantSlug = slug, email, password = "password123" });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await login.Content.ReadFromJsonAsync<LoginChallenge>())!;
    }

    [Fact]
    public async Task Enroll_ThenLogin_RequiresChallenge_AndTotpCodeIssuesTokens()
    {
        var client = await SignupOwner("tfa-login", "o@tfa-login.test");
        var (secret, _) = await Enroll(client);

        var challenge = await Login("tfa-login", "o@tfa-login.test");
        challenge.TwoFactorRequired.Should().BeTrue();
        challenge.ChallengeToken.Should().NotBeNullOrEmpty();

        var verify = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/2fa/verify",
            new { challengeToken = challenge.ChallengeToken, code = CurrentCode(secret) });
        verify.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokens = (await verify.Content.ReadFromJsonAsync<Tokens>())!;
        tokens.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task BackupCode_CompletesChallenge_AndIsSingleUse()
    {
        var client = await SignupOwner("tfa-backup", "o@tfa-backup.test");
        var (_, backupCodes) = await Enroll(client);
        var code = backupCodes[0];

        var challenge = await Login("tfa-backup", "o@tfa-backup.test");
        var verify = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/2fa/verify",
            new { challengeToken = challenge.ChallengeToken, code });
        verify.StatusCode.Should().Be(HttpStatusCode.OK);

        // The same backup code can't be reused on a fresh challenge.
        var challenge2 = await Login("tfa-backup", "o@tfa-backup.test");
        var reuse = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/2fa/verify",
            new { challengeToken = challenge2.ChallengeToken, code });
        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Verify_WrongCode_Returns401()
    {
        var client = await SignupOwner("tfa-wrong", "o@tfa-wrong.test");
        await Enroll(client);

        var challenge = await Login("tfa-wrong", "o@tfa-wrong.test");
        var verify = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/2fa/verify",
            new { challengeToken = challenge.ChallengeToken, code = "000000" });
        verify.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Disable_RequiresPassword_AndRestoresNormalLogin()
    {
        var client = await SignupOwner("tfa-disable", "o@tfa-disable.test");
        await Enroll(client);

        var wrong = await client.PostAsJsonAsync("/api/v1/account/2fa/disable", new { password = "wrong-pass" });
        wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var ok = await client.PostAsJsonAsync("/api/v1/account/2fa/disable", new { password = "password123" });
        ok.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Login returns tokens directly again (no challenge).
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { tenantSlug = "tfa-disable", email = "o@tfa-disable.test", password = "password123" });
        var tokens = (await login.Content.ReadFromJsonAsync<Tokens>())!;
        tokens.AccessToken.Should().NotBeNullOrEmpty();
    }
}
