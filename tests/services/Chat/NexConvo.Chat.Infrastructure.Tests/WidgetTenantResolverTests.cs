using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using NexConvo.Chat.Domain.Entities;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Chat.Infrastructure.Persistence;
using NexConvo.Contracts.Enums;

namespace NexConvo.Chat.Infrastructure.Tests;

/// <summary>
/// Minimal IConfiguration exposing only ConnectionStrings:ChatDb for the resolver.
/// GetConnectionString("ChatDb") resolves to GetSection("ConnectionStrings")["ChatDb"].
/// </summary>
internal sealed class StubConfiguration(string chatDbConnectionString) : IConfiguration
{
    private readonly Dictionary<string, string?> _values = new()
    {
        ["ConnectionStrings:ChatDb"] = chatDbConnectionString,
    };

    public string? this[string key]
    {
        get => _values.GetValueOrDefault(key);
        set => throw new NotSupportedException();
    }

    public IEnumerable<IConfigurationSection> GetChildren() => [];
    public IChangeToken GetReloadToken() => throw new NotSupportedException();
    public IConfigurationSection GetSection(string key) => new StubSection(key, _values);
}

internal sealed class StubSection(string path, Dictionary<string, string?> values) : IConfigurationSection
{
    public string? this[string key]
    {
        get => values.GetValueOrDefault($"{path}:{key}");
        set => throw new NotSupportedException();
    }

    public string Key => path;
    public string Path => path;
    public string? Value
    {
        get => values.GetValueOrDefault(path);
        set => throw new NotSupportedException();
    }

    public IEnumerable<IConfigurationSection> GetChildren() => [];
    public IChangeToken GetReloadToken() => throw new NotSupportedException();
    public IConfigurationSection GetSection(string key) => new StubSection($"{path}:{key}", values);
}

/// <summary>
/// The single RLS-exempt lookup on the anonymous widget path (Standard 6): resolve a public
/// <c>WidgetToken</c> to its owning tenant, but ONLY when that tenant has an active Web channel.
/// Backed by a Postgres SECURITY DEFINER function so the service role never bypasses RLS on the
/// table itself. These tests run on the RLS-enforced <c>nexconvo_service</c> connection — the same
/// role production uses — so a passing test proves the function (not a superuser) does the work.
/// </summary>
[Collection("chat-postgres")]
public sealed class WidgetTenantResolverTests(ChatPostgresFixture fixture)
{
    private static WorkspaceChatSettings NewSettings(Guid tenantId) =>
        new(tenantId, AiProviderType.OpenAI, "gpt-4o-mini", "[]", null, 0.5, false,
            SentimentSensitivity.Medium, "[]", 3, PiiMaskingLevel.Off, null);

    private async Task SeedAsync(Guid tenantId, Guid widgetToken, bool webChannelActive)
    {
        // Seed cross-tenant fixtures on a tenant-scoped context per tenant so RLS WITH CHECK passes.
        await using (var db = fixture.CreateTenantContext(tenantId))
        {
            var settings = NewSettings(tenantId);
            db.WorkspaceChatSettings.Add(settings);
            if (webChannelActive)
            {
                db.ChannelConnections.Add(new ChannelConnection(
                    tenantId, ChatChannel.Web, externalAccountId: "web", accountName: "Web",
                    encryptedAccessToken: "enc", isActive: true));
            }
            await db.SaveChangesAsync();
        }

        // Pin a known WidgetToken via the superuser connection (bypasses RLS) — the token is
        // assigned at random by the entity, so set it deterministically for the assertion.
        await using var admin = new Npgsql.NpgsqlConnection(fixture.SuperuserConnectionString);
        await admin.OpenAsync();
        await using var cmd = admin.CreateCommand();
        cmd.CommandText = "UPDATE workspace_chat_settings SET widget_token = @t WHERE tenant_id = @tid";
        cmd.Parameters.AddWithValue("t", widgetToken);
        cmd.Parameters.AddWithValue("tid", tenantId);
        await cmd.ExecuteNonQueryAsync();
    }

    private WidgetTenantResolver CreateResolver() =>
        new(new StubConfiguration(fixture.ServiceConnectionString));

    [Fact]
    public async Task ResolveAsync_KnownToken_WithActiveWebChannel_ReturnsTenant()
    {
        var tenantId = Guid.NewGuid();
        var token = Guid.NewGuid();
        await SeedAsync(tenantId, token, webChannelActive: true);

        var resolved = await CreateResolver().ResolveAsync(token, CancellationToken.None);

        resolved.Should().Be(tenantId);
    }

    [Fact]
    public async Task ResolveAsync_KnownToken_WithoutActiveWebChannel_ReturnsNull()
    {
        var tenantId = Guid.NewGuid();
        var token = Guid.NewGuid();
        await SeedAsync(tenantId, token, webChannelActive: false);

        var resolved = await CreateResolver().ResolveAsync(token, CancellationToken.None);

        resolved.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_UnknownToken_ReturnsNull()
    {
        var resolved = await CreateResolver().ResolveAsync(Guid.NewGuid(), CancellationToken.None);

        resolved.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_EmptyToken_ReturnsNull()
    {
        var resolved = await CreateResolver().ResolveAsync(Guid.Empty, CancellationToken.None);

        resolved.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_CannotReadAnotherTenantsToken_ViaTableScan()
    {
        // Two tenants each with a token; resolving one must never surface the other's tenant.
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var tokenA = Guid.NewGuid();
        var tokenB = Guid.NewGuid();
        await SeedAsync(tenantA, tokenA, webChannelActive: true);
        await SeedAsync(tenantB, tokenB, webChannelActive: true);

        var resolver = CreateResolver();

        (await resolver.ResolveAsync(tokenA, CancellationToken.None)).Should().Be(tenantA);
        (await resolver.ResolveAsync(tokenB, CancellationToken.None)).Should().Be(tenantB);
    }
}
