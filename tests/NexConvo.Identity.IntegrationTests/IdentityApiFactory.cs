using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NexConvo.Identity.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace NexConvo.Identity.IntegrationTests;

/// <summary>
/// Boots the Identity API against throwaway PostgreSQL + Mailpit containers. Migrations run as
/// the superuser; the app connects as the non-superuser <c>nexconvo_service</c> so RLS is
/// actually enforced (mirrors production). The platform-default email sender points at Mailpit.
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

    private readonly IContainer _mailpit = new ContainerBuilder()
        .WithImage("axllent/mailpit:latest")
        .WithPortBinding(1025, assignRandomHostPort: true)
        .WithPortBinding(8025, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8025).ForPath("/readyz")))
        .Build();

    /// <summary>Base URL of the Mailpit REST API (for asserting delivered mail in tests).</summary>
    public string MailpitApiBaseUrl => $"http://localhost:{_mailpit.GetMappedPublicPort(8025)}";

    public int MailpitSmtpPort => _mailpit.GetMappedPublicPort(1025);

    private string ServiceConnectionString =>
        $"Host=localhost;Port={_postgres.GetMappedPublicPort(5432)};Database=nexconvo_identity;" +
        $"Username=nexconvo_service;Password={ServicePassword}";

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _mailpit.StartAsync());

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
                ["Email:Default:Smtp:Host"] = "localhost",
                ["Email:Default:Smtp:Port"] = MailpitSmtpPort.ToString(),
                // The factory migrates explicitly (as superuser) in InitializeAsync.
                ["Database:AutoMigrate"] = "false",
                // Most tests signup-then-write without verifying; the dedicated guard test
                // re-enables this via WithWebHostBuilder.
                ["Auth:RequireVerifiedEmailForWrites"] = "false",
            }));
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _mailpit.DisposeAsync();
        await base.DisposeAsync();
    }
}
