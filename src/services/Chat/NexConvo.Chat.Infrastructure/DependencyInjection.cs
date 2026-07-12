using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexConvo.BuildingBlocks.Application.Health;
using NexConvo.BuildingBlocks.Application.Security;
using NexConvo.BuildingBlocks.Infrastructure.Security;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Resilience;
using NexConvo.Chat.Application.Common;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Application.Features.Health;
using NexConvo.Chat.Infrastructure.ExternalServices;
using NexConvo.Chat.Infrastructure.HealthCheck;
using NexConvo.Chat.Infrastructure.Persistence;
using NexConvo.Chat.Infrastructure.Services;
using NexConvo.Knowledge.Api.Grpc;

namespace NexConvo.Chat.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddChatInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ChatDb")
            ?? throw new InvalidOperationException("Connection string 'ChatDb' is not configured.");

        services.AddDbContext<ChatDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.EnableRetryOnFailure(3)));

        services.AddScoped<IChatDbContext>(sp => sp.GetRequiredService<ChatDbContext>());

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

        // First gRPC client in the solution — calls Knowledge's internal-only KnowledgeRetrieval.Search
        // (Standard 8: Polly resilience via AddNexConvoResilience, which composes with AddGrpcClient
        // since both return IHttpClientBuilder). Chat compiles its own copy of knowledge.proto
        // (GrpcServices="Client") rather than project-referencing Knowledge.Api — see Protos/knowledge.proto.
        var knowledgeGrpcAddress = configuration["Knowledge:GrpcAddress"]
            ?? throw new InvalidOperationException("Configuration 'Knowledge:GrpcAddress' is not set.");
        var internalApiKey = configuration["Internal:ApiKey"]
            ?? throw new InvalidOperationException("Configuration 'Internal:ApiKey' is not set.");

        services.AddGrpcClient<KnowledgeRetrieval.KnowledgeRetrievalClient>(o =>
            o.Address = new Uri(knowledgeGrpcAddress))
            .AddNexConvoResilience();

        services.AddScoped<IKnowledgeRetrievalClient>(sp =>
            new KnowledgeRetrievalClient(
                sp.GetRequiredService<KnowledgeRetrieval.KnowledgeRetrievalClient>(),
                sp.GetRequiredService<ITenantContext>(),
                internalApiKey));

        services.AddSingleton<NexConvo.BuildingBlocks.Rag.IGroundedPromptAssembler, NexConvo.BuildingBlocks.Rag.GroundedPromptAssembler>();
        services.AddSingleton<NexConvo.BuildingBlocks.Rag.ITokenBudgeter, NexConvo.BuildingBlocks.Rag.TokenBudgeter>();
        services.AddScoped<NexConvo.Chat.Application.Rag.IReplyOrchestrator, NexConvo.Chat.Application.Rag.ReplyOrchestrator>();

        services.AddMassTransit(x =>
        {
            // EF outbox: SaveChangesAsync + event publish commit atomically (Standard 10 — first
            // use in the solution). UseBusOutbox also enables the EF inbox, deduping consumers by
            // the transport message id (Standard 18).
            x.AddEntityFrameworkOutbox<ChatDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
            });

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
