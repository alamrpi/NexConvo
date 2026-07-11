using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexConvo.BuildingBlocks.Ai.Services;
using NexConvo.Knowledge.Infrastructure.Multitenancy;
using NexConvo.Knowledge.Infrastructure.Persistence;
using NSubstitute;
using Testcontainers.PostgreSql;

namespace NexConvo.Knowledge.IntegrationTests;

/// <summary>
/// Boots the Knowledge API (REST + the internal gRPC service) against a throwaway pgvector
/// Postgres, mirroring IdentityApiFactory. Migrations run explicitly here (as superuser) rather
/// than via Program.cs's Development-only auto-migrate, so the factory stays in the default
/// test environment and controls migration timing precisely. The active embedding provider is
/// swapped for a deterministic in-memory double — gRPC retrieval tests assert ranking behavior,
/// not the real BGE-M3 HTTP call (already covered by Slice 1).
/// </summary>
public sealed class KnowledgeApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestInternalApiKey = "test-internal-api-key";

    private const string ServicePassword = "svc_pw";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg16")
        .WithDatabase("nexconvo_knowledge")
        .WithUsername("nexconvo")
        .WithPassword("superuser_pw")
        .Build();

    public IEmbeddingProviderService EmbeddingProviderMock { get; } = Substitute.For<IEmbeddingProviderService>();

    private string ServiceConnectionString =>
        $"Host=localhost;Port={_postgres.GetMappedPublicPort(5432)};Database=nexconvo_knowledge;" +
        $"Username=nexconvo_service;Password={ServicePassword}";

    /// <summary>Superuser connection — Hangfire's schema installer needs DDL rights the RLS-restricted service role doesn't have.</summary>
    private string MigratorConnectionString =>
        $"Host=localhost;Port={_postgres.GetMappedPublicPort(5432)};Database=nexconvo_knowledge;" +
        "Username=nexconvo;Password=superuser_pw";

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _postgres.ExecScriptAsync($"CREATE ROLE nexconvo_service LOGIN PASSWORD '{ServicePassword}';");

        var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseNpgsql(_postgres.GetConnectionString(), npgsql => npgsql.UseVector())
            .Options;
        await using var context = new KnowledgeDbContext(options, new NullTenantContext());
        await context.Database.MigrateAsync();

        // WebApplicationFactory's ConfigureWebHost -> ConfigureAppConfiguration hook does not take
        // effect for this minimal-hosting-model app's HostFactoryResolver bootstrap path, so the
        // connection string and other test-only settings are supplied via process environment
        // variables instead — Program.cs's WebApplication.CreateBuilder always reads these
        // regardless of the testing host wrapper. Set before EnsureServer() triggers Program.Main.
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "IntegrationTest");
        Environment.SetEnvironmentVariable("ConnectionStrings__KnowledgeDb", ServiceConnectionString);
        Environment.SetEnvironmentVariable("ConnectionStrings__KnowledgeDbMigrator", MigratorConnectionString);
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", "");
        Environment.SetEnvironmentVariable("Internal__ApiKey", TestInternalApiKey);
        Environment.SetEnvironmentVariable("EMBEDDING__PROVIDER", "BgeM3");
    }

    /// <summary>A superuser context for direct test seeding (bypasses RLS, mirrors KnowledgePostgresFixture).</summary>
    public KnowledgeDbContext CreateSeedContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseNpgsql(ServiceConnectionString, npgsql => npgsql.UseVector())
            .Options;
        return new KnowledgeDbContext(options, new FixedTenantContext(tenantId));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Swap the real BGE-M3 HTTP-backed provider for a deterministic double so retrieval
            // tests assert ranking/tenant-isolation behavior without a live embedding server.
            services.AddSingleton<IEmbeddingProviderFactory>(_ =>
            {
                var factory = Substitute.For<IEmbeddingProviderFactory>();
                factory.GetActiveProvider().Returns(EmbeddingProviderMock);
                return factory;
            });
        });
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
