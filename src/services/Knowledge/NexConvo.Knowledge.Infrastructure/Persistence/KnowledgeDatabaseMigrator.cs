using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NexConvo.Knowledge.Infrastructure.Persistence;

public static class KnowledgeDatabaseMigrator
{
    public static async Task MigrateAsync(IConfiguration configuration, IHostEnvironment environment, ILogger logger)
    {
        if (!environment.IsDevelopment()) return;

        // Prefer the privileged migrator role (DDL + RLS + GRANT rights); fall back to the
        // RLS-enforced service role only when no migrator string is configured.
        var connectionString = configuration.GetConnectionString("KnowledgeDbMigrator")
                            ?? configuration.GetConnectionString("KnowledgeDb");

        if (string.IsNullOrEmpty(connectionString)) return;

        logger.LogInformation("Applying Knowledge database migrations...");

        var optionsBuilder = new DbContextOptionsBuilder<KnowledgeDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql => npgsql.UseVector());

        using var dbContext = new KnowledgeDbContext(optionsBuilder.Options, new NullTenantContext());
        await dbContext.Database.MigrateAsync();

        logger.LogInformation("Knowledge database is up to date.");
    }
}
