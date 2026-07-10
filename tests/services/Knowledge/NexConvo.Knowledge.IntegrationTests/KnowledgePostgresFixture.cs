using Microsoft.EntityFrameworkCore;
using NexConvo.Knowledge.Infrastructure.Multitenancy;
using NexConvo.Knowledge.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace NexConvo.Knowledge.IntegrationTests;

/// <summary>
/// Throwaway pgvector Postgres for the Knowledge service. Migrations run as the container's
/// superuser; test contexts connect as the non-superuser <c>nexconvo_service</c> so RLS is
/// actually enforced (a superuser bypasses RLS, which would make every isolation test pass
/// vacuously). Mirrors IdentityApiFactory.
/// </summary>
public sealed class KnowledgePostgresFixture : IAsyncLifetime
{
    private const string ServicePassword = "svc_pw";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg16")
        .WithDatabase("nexconvo_knowledge")
        .WithUsername("nexconvo")
        .WithPassword("superuser_pw")
        .Build();

    /// <summary>Superuser connection — bypasses RLS; for schema asserts and seeding checks only.</summary>
    public string SuperuserConnectionString => _postgres.GetConnectionString();

    /// <summary>RLS-enforced connection the service would use in production.</summary>
    public string ServiceConnectionString =>
        $"Host=localhost;Port={_postgres.GetMappedPublicPort(5432)};Database=nexconvo_knowledge;" +
        $"Username=nexconvo_service;Password={ServicePassword}";

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // The app role must exist BEFORE migrating — the migration GRANTs to it.
        await _postgres.ExecScriptAsync($"CREATE ROLE nexconvo_service LOGIN PASSWORD '{ServicePassword}';");

        var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseNpgsql(SuperuserConnectionString, npgsql => npgsql.UseVector())
            .Options;
        await using var context = new KnowledgeDbContext(options, new NullTenantContext());
        await context.Database.MigrateAsync();
    }

    /// <summary>
    /// A context pinned to <paramref name="tenantId"/> on the RLS-enforced service connection —
    /// the RlsConnectionInterceptor sets app.current_tenant_id on every connection open.
    /// </summary>
    public KnowledgeDbContext CreateTenantContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseNpgsql(ServiceConnectionString, npgsql => npgsql.UseVector())
            .Options;
        return new KnowledgeDbContext(options, new FixedTenantContext(tenantId));
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();
}

[CollectionDefinition("knowledge-postgres")]
public sealed class KnowledgePostgresCollectionDefinition : ICollectionFixture<KnowledgePostgresFixture>;
