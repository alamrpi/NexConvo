using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace NexConvo.Identity.IntegrationTests;

/// <summary>
/// Exercises the emailed auth flows end-to-end: the real email lands in Mailpit, the token is
/// extracted from the link, and the flow is completed against the API.
/// </summary>
public sealed partial class AuthEmailFlowTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    private sealed record Tokens(string AccessToken, int ExpiresInSeconds, string RefreshToken);

    private static async Task<Tokens> Signup(HttpClient client, string slug, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/signup", new
        {
            tenantName = "Acme",
            tenantSlug = slug,
            email,
            password = "password123",
            fullName = "Owner",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<Tokens>())!;
    }

    [Fact]
    public async Task Signup_SendsVerificationEmail_AndTokenVerifies()
    {
        var client = factory.CreateClient();
        await Signup(client, "verify-flow", "owner@verify.com");

        var token = await ExtractToken("owner@verify.com", "/verify-email");
        var verify = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new { token });

        verify.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ForgotPassword_ResetsPassword_AndOldPasswordStopsWorking()
    {
        var client = factory.CreateClient();
        await Signup(client, "reset-flow", "owner@reset.com");

        var forgot = await client.PostAsJsonAsync("/api/v1/auth/forgot-password",
            new { tenantSlug = "reset-flow", email = "owner@reset.com" });
        forgot.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var token = await ExtractToken("owner@reset.com", "/reset-password");
        var reset = await client.PostAsJsonAsync("/api/v1/auth/reset-password",
            new { token, newPassword = "brand-new-pass-123" });
        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var withNew = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { tenantSlug = "reset-flow", email = "owner@reset.com", password = "brand-new-pass-123" });
        withNew.StatusCode.Should().Be(HttpStatusCode.OK);

        var withOld = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { tenantSlug = "reset-flow", email = "owner@reset.com", password = "password123" });
        withOld.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Invite_AcceptedByTeammate_CreatesUsableAccount()
    {
        var owner = factory.CreateClient();
        var tokens = await Signup(owner, "invite-flow", "owner@invite.com");
        owner.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);

        var invite = await owner.PostAsJsonAsync("/api/v1/invitations",
            new { email = "teammate@invite.com", roleName = "Agent" });
        invite.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var token = await ExtractToken("teammate@invite.com", "/accept-invite");
        var accept = await factory.CreateClient().PostAsJsonAsync("/api/v1/invitations/accept",
            new { token, fullName = "Team Mate", password = "teammate-pass-123" });
        accept.StatusCode.Should().Be(HttpStatusCode.OK);

        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { tenantSlug = "invite-flow", email = "teammate@invite.com", password = "teammate-pass-123" });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
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
