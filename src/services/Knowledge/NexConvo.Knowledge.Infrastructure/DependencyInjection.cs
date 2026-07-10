using Hangfire;
using Hangfire.PostgreSql;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexConvo.Knowledge.Application.Common.Interfaces;
using NexConvo.Knowledge.Infrastructure.Jobs;
using NexConvo.Knowledge.Infrastructure.Persistence;

namespace NexConvo.Knowledge.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddKnowledgeInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("KnowledgeDb")
            ?? throw new InvalidOperationException("Connection string 'KnowledgeDb' is not configured.");

        // Register KnowledgeDbContext with pgvector support (CHATBOT-ARCHITECTURE.md §12)
        services.AddDbContext<KnowledgeDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.UseVector();
                npgsql.EnableRetryOnFailure(3);
            }));

        services.AddScoped<IKnowledgeDbContext>(sp => sp.GetRequiredService<KnowledgeDbContext>());

        // Hangfire needs schema-creation rights — use the owner/migrator user if available.
        var hangfireConn = configuration.GetConnectionString("KnowledgeDbMigrator") ?? connectionString;
        services.AddHangfire(config =>
            config.UsePostgreSqlStorage(c =>
                c.UseNpgsqlConnection(hangfireConn)));

        services.AddHangfireServer(options =>
        {
            options.WorkerCount = 4;
            options.Queues = ["knowledge-ingestion", "default"];
        });

        services.AddScoped<IKnowledgeIngestionJobRunner, KnowledgeIngestionJob>();
        services.AddScoped<IKnowledgeChunkWriter, KnowledgeChunkWriter>();

        services.AddMassTransit(x =>
        {
            x.AddConsumers(typeof(NexConvo.Knowledge.Application.DependencyInjection).Assembly);
            x.UsingRabbitMq((context, cfg) =>
            {
                var rmq = configuration.GetConnectionString("RabbitMQ") ?? "amqp://guest:guest@localhost:5672";
                cfg.Host(rmq);

                // Prefix every endpoint with the service name so fan-out commands consumed by
                // multiple services never land on one shared, competing queue.
                cfg.ConfigureEndpoints(context, new KebabCaseEndpointNameFormatter("knowledge", false));
            });
        });

        return services;
    }
}
