using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NexConvo.Identity.Infrastructure.Persistence;

/// <summary>
/// Applies pending EF migrations at startup when enabled. Uses a PRIVILEGED connection
/// (<c>ConnectionStrings:IdentityDbMigrator</c> — the table owner) because migrations run
/// DDL + GRANT + RLS that the runtime app role (<c>nexconvo_service</c>) is intentionally not
/// allowed to do. Enabled by default in Development; opt-in elsewhere via
/// <c>Database:AutoMigrate=true</c> (production should prefer a controlled migration job — see
/// OPERATIONS.md). Re-running is safe: it no-ops when there are no pending migrations, and EF's
/// migration lock serialises concurrent starters.
/// </summary>
public static class IdentityDatabaseMigrator
{
    public static async Task MigrateAsync(
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var autoMigrate = configuration.GetValue("Database:AutoMigrate", environment.IsDevelopment());
        if (!autoMigrate)
        {
            logger.LogInformation("Auto-migration disabled (Database:AutoMigrate).");
            return;
        }

        // Migrations need owner privileges; fall back to the runtime string only if no migrator
        // string is set (which will fail on DDL — surfaced clearly rather than silently).
        var connectionString = configuration.GetConnectionString("IdentityDbMigrator")
            ?? configuration.GetConnectionString("IdentityDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogWarning("Auto-migration enabled but no connection string configured; skipping.");
            return;
        }

        var options = new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(connectionString).Options;
        await using var context = new IdentityDbContext(options);

        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count == 0)
        {
            logger.LogInformation("Identity database is up to date; no migrations to apply.");
            return;
        }

        logger.LogInformation(
            "Applying {Count} Identity migration(s): {Migrations}", pending.Count, string.Join(", ", pending));
        await context.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Identity database migration complete.");
    }
}
