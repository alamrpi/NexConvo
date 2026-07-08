using Hangfire;
using Hangfire.PostgreSql;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexConvo.BuildingBlocks.Application.Health;
using NexConvo.BuildingBlocks.Application.Security;
using NexConvo.BuildingBlocks.Infrastructure.Security;
using NexConvo.BuildingBlocks.Resilience;
using NexConvo.Chat.Application.Common;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Application.Features.Health;
using NexConvo.Chat.Infrastructure.ExternalServices;
using NexConvo.Chat.Infrastructure.HealthCheck;
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

        // Resilient (Standard 8: Polly retry/circuit-breaker/timeout) named HttpClients for
        // channel connection verification. ChannelVerificationHttpClientFactory maps each
        // ChatChannel to the correct named client below.
        services.AddHttpClient("MetaGraphApi", c => c.BaseAddress = new Uri("https://graph.facebook.com/"))
            .AddNexConvoResilience();
        services.AddHttpClient("TelegramApi", c => c.BaseAddress = new Uri("https://api.telegram.org/"))
            .AddNexConvoResilience();
        services.AddHttpClient("ChannelVerification")
            .AddNexConvoResilience();

        services.AddSingleton<IChannelVerificationHttpClientFactory, ChannelVerificationHttpClientFactory>();
        services.AddScoped<IConnectionTester<ChannelTestInput>, ChannelConnectionTester>();
        services.AddScoped<IChatHealthSweepService, ChatHealthSweepService>();

        services.AddMassTransit(x =>
        {
            x.AddConsumers(typeof(NexConvo.Chat.Application.DependencyInjection).Assembly);
            x.UsingRabbitMq((context, cfg) =>
            {
                var rmq = configuration.GetConnectionString("RabbitMQ") ?? "amqp://guest:guest@localhost:5672";
                cfg.Host(rmq);

                // Prefix every endpoint with the service name. Without this, MassTransit's default
                // convention names a queue after the MESSAGE type — so CheckIntegrationHealthCommand
                // (a fan-out trigger BOTH Chat and Integrations must receive) would land both
                // services' consumers on the SAME queue as competing consumers, delivering each
                // publish to only one of them instead of both.
                cfg.ConfigureEndpoints(context, new KebabCaseEndpointNameFormatter("chat", false));
            });
        });

        return services;
    }
}
