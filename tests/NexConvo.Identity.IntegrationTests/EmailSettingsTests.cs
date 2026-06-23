using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace NexConvo.Identity.IntegrationTests;

public sealed class EmailSettingsTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    private sealed record Tokens(string AccessToken, int ExpiresInSeconds, string RefreshToken);

    private async Task<HttpClient> AuthedOwnerClient(string slug, string email)
    {
        var client = factory.CreateClient();
        var signup = await client.PostAsJsonAsync("/api/v1/auth/signup", new
        {
            tenantName = "Acme",
            tenantSlug = slug,
            email,
            password = "password123",
            fullName = "Owner",
        });
        signup.EnsureSuccessStatusCode();
        var tokens = await signup.Content.ReadFromJsonAsync<Tokens>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens!.AccessToken);
        return client;
    }

    private object SmtpSettingsBody(bool enabled) => new
    {
        provider = "Smtp",
        fromName = "Acme Support",
        fromAddress = "no-reply@acme.com",
        isEnabled = enabled,
        smtpHost = "localhost",
        smtpPort = factory.MailpitSmtpPort,
        smtpUsername = (string?)null,
        smtpUseSsl = false,
        secret = (string?)null,
    };

    [Fact]
    public async Task GetEmail_BeforeConfig_ReturnsNotConfiguredDefaults()
    {
        var client = await AuthedOwnerClient("email-default", "owner@email-default.com");

        var json = await client.GetFromJsonAsync<JsonElement>("/api/v1/settings/email");

        json.GetProperty("isEnabled").GetBoolean().Should().BeFalse();
        json.GetProperty("hasSecret").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task PutThenGet_PersistsSettings_AndNeverReturnsTheSecret()
    {
        var client = await AuthedOwnerClient("email-roundtrip", "owner@email-roundtrip.com");

        var put = await client.PutAsJsonAsync("/api/v1/settings/email", new
        {
            provider = "Resend",
            fromName = "Acme",
            fromAddress = "no-reply@acme.com",
            isEnabled = true,
            smtpHost = (string?)null,
            smtpPort = (int?)null,
            smtpUsername = (string?)null,
            smtpUseSsl = (bool?)null,
            secret = "re_live_super_secret_key",
        });
        put.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var raw = await (await client.GetAsync("/api/v1/settings/email")).Content.ReadAsStringAsync();
        raw.Should().Contain("\"hasSecret\":true");
        raw.ToLowerInvariant().Should().NotContain("re_live_super_secret_key");
        raw.ToLowerInvariant().Should().NotContain("encryptedsecret");
    }

    [Fact]
    public async Task SendTest_DeliversEmail_ViaResolvedSender()
    {
        const string ownerEmail = "owner@email-send.com";
        var client = await AuthedOwnerClient("email-send", ownerEmail);

        (await client.PutAsJsonAsync("/api/v1/settings/email", SmtpSettingsBody(enabled: true)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var test = await client.PostAsync("/api/v1/settings/email/test", content: null);
        test.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await MailpitDeliveredTo(ownerEmail)).Should().BeTrue("the test email should arrive in Mailpit");
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task EmailSettings_AreIsolatedPerTenant()
    {
        var tenantA = await AuthedOwnerClient("email-iso-a", "owner@iso-a.com");
        (await tenantA.PutAsJsonAsync("/api/v1/settings/email", SmtpSettingsBody(enabled: true)))
            .EnsureSuccessStatusCode();

        var tenantB = await AuthedOwnerClient("email-iso-b", "owner@iso-b.com");
        var bSettings = await tenantB.GetFromJsonAsync<JsonElement>("/api/v1/settings/email");

        bSettings.GetProperty("isEnabled").GetBoolean().Should().BeFalse("tenant B must not see tenant A's config");
    }

    private async Task<bool> MailpitDeliveredTo(string email)
    {
        using var http = new HttpClient { BaseAddress = new Uri(factory.MailpitApiBaseUrl) };
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var response = await http.GetFromJsonAsync<JsonElement>(
                $"/api/v1/search?query=to%3A{Uri.EscapeDataString(email)}");
            if (response.TryGetProperty("messages_count", out var count) && count.GetInt32() > 0)
            {
                return true;
            }

            await Task.Delay(300);
        }

        return false;
    }
}
