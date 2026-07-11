using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Hangfire;
using Hangfire.PostgreSql;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexConvo.BuildingBlocks.Ai;
using NexConvo.BuildingBlocks.Application.Security;
using NexConvo.BuildingBlocks.Infrastructure.Security;
using NexConvo.BuildingBlocks.Resilience;
using NexConvo.Knowledge.Application.Common.Interfaces;
using NexConvo.Knowledge.Infrastructure.Chunking;
using NexConvo.Knowledge.Infrastructure.ExternalServices;
using NexConvo.Knowledge.Infrastructure.Jobs;
using NexConvo.Knowledge.Infrastructure.Persistence;
using NexConvo.Knowledge.Infrastructure.TextExtraction;

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
        services.AddScoped<IKnowledgeChunkRepository, KnowledgeChunkRepository>();
        services.AddSingleton<IChunker, BengaliAwareChunker>();

        // Embedding provider used by the ingestion job to embed chunks (BGE-M3 default, Cohere
        // alternative) — same swappable abstraction Slice 1 introduced for chat/RAG.
        services.AddEmbeddingProviders(configuration);

        // Text extraction: each concrete extractor registered for the factory's constructor
        // injection, plus the factory itself resolved by MIME-type/file-extension (Standard 2 —
        // constructor injection only, no service-locator lookups).
        services.AddSingleton<PdfTextExtractor>();
        services.AddSingleton<DocxTextExtractor>();
        services.AddSingleton<PlainTextExtractor>();
        services.AddSingleton<ITextExtractorFactory, TextExtractorFactory>();

        // Redis-backed IDistributedCache: caches the Integrations-owned S3 config (Standard 5 —
        // database-per-service; Knowledge never queries Integrations' DB directly) for ingestion
        // to resolve bucket/credentials without a per-request cross-service call.
        var redisConn = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        services.AddStackExchangeRedisCache(o => o.Configuration = redisConn);

        services.AddSingleton<IAesEncryptionService, AesEncryptionService>();

        // Per-call AmazonS3Client factory built from decrypted creds — mirrors
        // NexConvo.Integrations.Infrastructure.ExternalServices.S3ConnectionTester's client
        // construction exactly. Never caches a client or decrypted secret across calls.
        services.AddSingleton<Func<S3StorageInput, IAmazonS3>>(_ => input =>
        {
            var creds = new BasicAWSCredentials(input.AccessKeyId, input.SecretAccessKey);
            var cfg = new AmazonS3Config();
            if (!string.IsNullOrWhiteSpace(input.CustomEndpoint))
            {
                cfg.ServiceURL = input.CustomEndpoint;
                cfg.ForcePathStyle = true;
            }
            else
            {
                cfg.RegionEndpoint = RegionEndpoint.GetBySystemName(input.Region);
            }
            return new AmazonS3Client(creds, cfg);
        });
        services.AddScoped<IS3StorageService, S3StorageService>();

        // Url-source ingestion: DNS resolution feeds the handler's SSRF allow-list guard (blocks
        // private/loopback/link-local ranges AFTER resolution, defeating DNS rebinding); the fetch
        // itself is a resilient (Polly), https-only, size-capped HttpClient (Standard 8).
        services.AddSingleton<IDnsResolver, DnsResolver>();
        services.AddHttpClient(KnowledgeHttpClientNames.UrlFetch)
            .AddNexConvoResilience();
        services.AddScoped<IUrlContentFetcher, UrlContentFetcher>();

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
