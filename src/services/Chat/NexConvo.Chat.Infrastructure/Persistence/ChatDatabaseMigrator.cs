using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NexConvo.Chat.Infrastructure.Persistence;

public static class ChatDatabaseMigrator
{
    public static async Task MigrateAsync(IConfiguration configuration, IHostEnvironment environment, ILogger logger)
    {
        if (!environment.IsDevelopment()) return;

        var connectionString = configuration.GetConnectionString("ChatDbMigrator")
                            ?? configuration.GetConnectionString("ChatDb");

        if (string.IsNullOrEmpty(connectionString)) return;

        logger.LogInformation("Applying Chat database migrations...");

        var optionsBuilder = new DbContextOptionsBuilder<ChatDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        using var dbContext = new ChatDbContext(optionsBuilder.Options, new NullTenantContext());
        await dbContext.Database.MigrateAsync();

        logger.LogInformation("Chat database is up to date.");
    }
}
