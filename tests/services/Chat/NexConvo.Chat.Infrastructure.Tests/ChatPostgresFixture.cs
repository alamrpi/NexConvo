using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.Chat.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace NexConvo.Chat.Infrastructure.Tests;

/// <summary>
/// Throwaway Postgres for the Chat service. Migrations run as the container's superuser; test
/// contexts connect as the non-superuser <c>nexconvo_service</c> so RLS is actually enforced (a
/// superuser bypasses RLS, which would make every isolation test pass vacuously). Mirrors
/// KnowledgePostgresFixture.
/// </summary>
public sealed class ChatPostgresFixture : IAsyncLifetime
{
    private const string ServicePassword = "svc_pw";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("nexconvo_chat")
        .WithUsername("nexconvo")
        .WithPassword("superuser_pw")
        .Build();

    /// <summary>Superuser connection — bypasses RLS; for schema asserts and seeding checks only.</summary>
    public string SuperuserConnectionString => _postgres.GetConnectionString();

    /// <summary>RLS-enforced connection the service would use in production.</summary>
    public string ServiceConnectionString =>
        $"Host=localhost;Port={_postgres.GetMappedPublicPort(5432)};Database=nexconvo_chat;" +
        $"Username=nexconvo_service;Password={ServicePassword}";

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // The app role must exist BEFORE migrating — the migration GRANTs to it.
        await _postgres.ExecScriptAsync($"CREATE ROLE nexconvo_service LOGIN PASSWORD '{ServicePassword}';");

        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseNpgsql(SuperuserConnectionString)
            .Options;
        await using var context = new ChatDbContext(options, new NullTenantContext());
        await context.Database.MigrateAsync();
    }

    /// <summary>
    /// A context pinned to <paramref name="tenantId"/> on the RLS-enforced service connection —
    /// the RlsConnectionInterceptor sets app.current_tenant_id on every connection open.
    /// </summary>
    public ChatDbContext CreateTenantContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseNpgsql(ServiceConnectionString)
            .Options;
        return new ChatDbContext(options, new FixedTenantContext(tenantId));
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();
}

[CollectionDefinition("chat-postgres")]
public sealed class ChatPostgresCollectionDefinition : ICollectionFixture<ChatPostgresFixture>;

/// <summary>A no-op ITenantContext for fixture-driven migrations that have no HTTP context.</summary>
internal sealed class NullTenantContext : ITenantContext
{
    public bool HasTenant => false;
    public Guid TenantId => throw new InvalidOperationException(
        "No tenant context is available outside an HTTP request scope.");
}

/// <summary>
/// An ITenantContext pinned to one tenant, letting the RLS interceptor scope the connection
/// exactly as it would for an HTTP request — RLS stays enforced instead of being bypassed with an
/// owner connection.
/// </summary>
internal sealed class FixedTenantContext(Guid tenantId) : ITenantContext
{
    public bool HasTenant => true;
    public Guid TenantId => tenantId;
}
