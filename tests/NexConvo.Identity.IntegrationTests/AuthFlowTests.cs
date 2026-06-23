using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace NexConvo.Identity.IntegrationTests;

public sealed class AuthFlowTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    private sealed record Tokens(string AccessToken, int ExpiresInSeconds, string RefreshToken);

    private sealed record Me(
        Guid UserId, Guid TenantId, string TenantSlug, string Email, string FullName,
        string[] Roles, string[] Permissions);

    private static object SignupBody(string slug, string email) => new
    {
        tenantName = "Acme Inc",
        tenantSlug = slug,
        email,
        password = "password123",
        fullName = "Jane Owner",
    };

    [Fact]
    public async Task Signup_Login_Me_HappyPath()
    {
        var client = factory.CreateClient();

        var signup = await client.PostAsJsonAsync("/api/v1/auth/signup", SignupBody("acme-happy", "owner@acme.com"));
        signup.StatusCode.Should().Be(HttpStatusCode.Created);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { tenantSlug = "acme-happy", email = "owner@acme.com", password = "password123" });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokens = await login.Content.ReadFromJsonAsync<Tokens>();
        tokens!.AccessToken.Should().NotBeNullOrEmpty();

        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        var me = await client.GetFromJsonAsync<Me>("/api/v1/auth/me");

        me!.TenantSlug.Should().Be("acme-happy");
        me.Email.Should().Be("owner@acme.com");
        me.Roles.Should().Contain("Owner");
        me.Permissions.Should().Contain("*");
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefreshToken_Rotates_AndToleratesConcurrentReuseWithinGrace()
    {
        var client = factory.CreateClient();

        var signup = await client.PostAsJsonAsync("/api/v1/auth/signup", SignupBody("acme-refresh", "r@acme.com"));
        var first = await signup.Content.ReadFromJsonAsync<Tokens>();

        var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = first!.RefreshToken });
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotated = await refresh.Content.ReadFromJsonAsync<Tokens>();
        rotated!.RefreshToken.Should().NotBe(first.RefreshToken);

        // Reusing the just-rotated token (a racing client around access-token expiry) is tolerated
        // within the grace window: it returns a fresh, usable token instead of logging the user out.
        var reuse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = first.RefreshToken });
        reuse.StatusCode.Should().Be(HttpStatusCode.OK);
        var regraced = await reuse.Content.ReadFromJsonAsync<Tokens>();
        regraced!.RefreshToken.Should().NotBe(first.RefreshToken);

        var stillWorks = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = regraced.RefreshToken });
        stillWorks.StatusCode.Should().Be(HttpStatusCode.OK);

        // A bogus / never-issued token is still rejected.
        var bogus = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = "Zm9v.YmFy" });
        bogus.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Jwks_PublishesSigningKey()
    {
        var client = factory.CreateClient();
        var jwks = await client.GetFromJsonAsync<JsonElement>("/.well-known/jwks.json");

        var key = jwks.GetProperty("keys")[0];
        key.GetProperty("kty").GetString().Should().Be("RSA");
        key.GetProperty("kid").GetString().Should().NotBeNullOrEmpty();
        key.GetProperty("alg").GetString().Should().Be("RS256");
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task SameEmail_InTwoTenants_ResolvesToDistinctUsers()
    {
        var client = factory.CreateClient();
        const string email = "shared@example.com";

        (await client.PostAsJsonAsync("/api/v1/auth/signup", SignupBody("iso-a", email))).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/v1/auth/signup", SignupBody("iso-b", email))).EnsureSuccessStatusCode();

        var meA = await LoginAndGetMe(client, "iso-a", email);
        var meB = await LoginAndGetMe(client, "iso-b", email);

        meA.TenantSlug.Should().Be("iso-a");
        meB.TenantSlug.Should().Be("iso-b");
        meA.UserId.Should().NotBe(meB.UserId);
        meA.TenantId.Should().NotBe(meB.TenantId);
    }

    private static async Task<Me> LoginAndGetMe(HttpClient client, string slug, string email)
    {
        var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { tenantSlug = slug, email, password = "password123" });
        login.EnsureSuccessStatusCode();
        var tokens = await login.Content.ReadFromJsonAsync<Tokens>();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Authorization = new("Bearer", tokens!.AccessToken);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Me>())!;
    }
}
