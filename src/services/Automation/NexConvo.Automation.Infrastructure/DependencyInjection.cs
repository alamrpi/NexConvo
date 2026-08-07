using Hangfire;
using Hangfire.PostgreSql;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexConvo.Automation.Infrastructure.Jobs;

namespace NexConvo.Automation.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAutomationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Automation holds no per-tenant config state — it only needs a Postgres database for
        // Hangfire's own job storage. The migrator connection (owner/creator rights) is required
        // because Hangfire provisions its schema on first run, same reason Chat uses ChatDbMigrator.
        var hangfireConn = configuration.GetConnectionString("AutomationDbMigrator")
            ?? configuration.GetConnectionString("AutomationDb")
            ?? throw new InvalidOperationException(
                "Connection string 'AutomationDbMigrator' (or 'AutomationDb') is not configured.");

        services.AddHangfire(config =>
            config.UsePostgreSqlStorage(c =>
                c.UseNpgsqlConnection(hangfireConn)));

        services.AddHangfireServer(options =>
        {
            options.Queues = ["default"];
        });

        services.AddScoped<IntegrationHealthSweepScheduler>();

        // Publish-only bus: Automation triggers the sweep but never consumes messages, so there
        // are no AddConsumers/ConfigureEndpoints calls here.
        services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((_, cfg) =>
            {
                var rmq = configuration.GetConnectionString("RabbitMQ") ?? "amqp://guest:guest@localhost:5672";
                cfg.Host(rmq);
            });
        });

        return services;
    }
}
