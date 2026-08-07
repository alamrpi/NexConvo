using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MassTransit;
using NexConvo.BuildingBlocks.Application.Health;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.Integrations.Application;
using NexConvo.Integrations.Application.Features.AiConfig;
using NexConvo.Integrations.Application.Features.Health;
using NexConvo.Integrations.Application.Features.S3Config;
using NexConvo.Integrations.Infrastructure.ExternalServices;
using NexConvo.Integrations.Infrastructure.HealthCheck;
using NexConvo.Integrations.Infrastructure.Persistence;

namespace NexConvo.Integrations.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIntegrationsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("IntegrationsDb");
        
        services.AddNexConvoTenancy();
        services.AddDbContext<IntegrationsDbContext>((sp, options) =>
            options.UseNpgsql(connectionString)
                   .AddInterceptors(sp.GetRequiredService<NexConvo.BuildingBlocks.Multitenancy.RlsConnectionInterceptor>()));

        services.AddScoped<IIntegrationsDbContext>(provider => provider.GetRequiredService<IntegrationsDbContext>());

        services.AddSingleton<NexConvo.BuildingBlocks.Application.Security.IAesEncryptionService, NexConvo.BuildingBlocks.Infrastructure.Security.AesEncryptionService>();

        services.AddMassTransit(x =>
        {
            x.AddConsumers(typeof(NexConvo.Integrations.Application.DependencyInjection).Assembly);

            x.UsingRabbitMq((context, cfg) =>
            {
                var rmq = configuration.GetConnectionString("RabbitMQ") ?? "amqp://guest:guest@localhost:5672";
                cfg.Host(rmq);

                // Prefix every endpoint with the service name. Without this, MassTransit's default
                // convention names a queue after the MESSAGE type — so CheckIntegrationHealthCommand
                // (a fan-out trigger BOTH Chat and Integrations must receive) would land both
                // services' consumers on the SAME queue as competing consumers, delivering each
                // publish to only one of them instead of both.
                cfg.ConfigureEndpoints(context, new KebabCaseEndpointNameFormatter("integrations", false));
            });
        });

        services.AddScoped<IIntegrationsHealthSweepService, IntegrationsHealthSweepService>();

        services.AddSingleton<Func<S3TestInput, IAmazonS3>>(_ => input =>
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
        services.AddScoped<IConnectionTester<S3TestInput>, S3ConnectionTester>();
        services.AddScoped<IConnectionTester<AiTestInput>, AiConnectionTester>();

        return services;
    }
}
