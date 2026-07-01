using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NexConvo.Integrations.Infrastructure.Persistence;

public static class IntegrationsDatabaseMigrator
{
    public static async Task MigrateAsync(IConfiguration configuration, IHostEnvironment environment, ILogger logger)
    {
        if (!environment.IsDevelopment()) return;

        var connectionString = configuration.GetConnectionString("IntegrationsDbMigrator") 
                            ?? configuration.GetConnectionString("IntegrationsDb");
        
        if (string.IsNullOrEmpty(connectionString)) return;

        logger.LogInformation("Applying Integrations database migrations...");
        
        var optionsBuilder = new DbContextOptionsBuilder<IntegrationsDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        using var dbContext = new IntegrationsDbContext(optionsBuilder.Options);
        await dbContext.Database.MigrateAsync();
        
        logger.LogInformation("Integrations database is up to date.");
    }
}
