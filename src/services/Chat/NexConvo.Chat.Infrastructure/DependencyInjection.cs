using Hangfire;
using Hangfire.PostgreSql;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexConvo.BuildingBlocks.Application.Security;
using NexConvo.BuildingBlocks.Infrastructure.Security;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Infrastructure.Jobs;
using NexConvo.Chat.Infrastructure.Persistence;
using NexConvo.Chat.Infrastructure.Services;

namespace NexConvo.Chat.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddChatInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ChatDb")
            ?? throw new InvalidOperationException("Connection string 'ChatDb' is not configured.");

        // Register ChatDbContext with pgvector support (CHATBOT-ARCHITECTURE.md §12)
        services.AddDbContext<ChatDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.UseVector();
                npgsql.EnableRetryOnFailure(3);
            }));

        services.AddScoped<IChatDbContext>(sp => sp.GetRequiredService<ChatDbContext>());

        // Hangfire needs schema-creation rights — use the owner/migrator user if available.
        var hangfireConn = configuration.GetConnectionString("ChatDbMigrator") ?? connectionString;
        services.AddHangfire(config =>
            config.UsePostgreSqlStorage(c =>
                c.UseNpgsqlConnection(hangfireConn)));

        services.AddHangfireServer(options =>
        {
            options.WorkerCount = 4;
            options.Queues = ["knowledge-ingestion", "default"];
        });

        // Register the ingestion job as a scoped service so it gets IChatDbContext injected
        services.AddScoped<IKnowledgeIngestionJobRunner, KnowledgeIngestionJob>();

        var redisConn = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        services.AddStackExchangeRedisCache(o => o.Configuration = redisConn);

        services.AddSingleton<IAesEncryptionService, AesEncryptionService>();
        services.AddSingleton<IChannelConnectionTester, ChannelConnectionTester>();

        services.AddMassTransit(x =>
        {
            x.AddConsumers(typeof(NexConvo.Chat.Application.DependencyInjection).Assembly);
            x.UsingRabbitMq((context, cfg) =>
            {
                var rmq = configuration.GetConnectionString("RabbitMQ") ?? "amqp://guest:guest@localhost:5672";
                cfg.Host(rmq);
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
