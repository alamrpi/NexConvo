using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NexConvo.Identity.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace NexConvo.Identity.IntegrationTests;

/// <summary>
/// Boots the Identity API against a throwaway PostgreSQL container. Migrations run as the
/// superuser; the app connects as the non-superuser <c>nexconvo_service</c> so RLS is actually
/// enforced (mirrors production).
/// </summary>
public sealed class IdentityApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string ServicePassword = "svc_pw";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("nexconvo_identity")
        .WithUsername("nexconvo")
        .WithPassword("superuser_pw")
        .Build();

    private string ServiceConnectionString =>
        $"Host=localhost;Port={_postgres.GetMappedPublicPort(5432)};Database=nexconvo_identity;" +
        $"Username=nexconvo_service;Password={ServicePassword}";

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Create the non-superuser app role, then migrate as the superuser (which GRANTs to it + enables RLS).
        await _postgres.ExecScriptAsync($"CREATE ROLE nexconvo_service LOGIN PASSWORD '{ServicePassword}';");

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var context = new IdentityDbContext(options);
        await context.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development"); // ephemeral RSA dev key + RequireHttpsMetadata=false
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:IdentityDb"] = ServiceConnectionString,
            }));
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
