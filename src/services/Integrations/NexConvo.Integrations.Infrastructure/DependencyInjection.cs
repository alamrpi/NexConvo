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
using NexConvo.Integrations.Application.Features.S3Config;
using NexConvo.Integrations.Infrastructure.ExternalServices;
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
            x.UsingRabbitMq((context, cfg) =>
            {
                var rmq = configuration.GetConnectionString("RabbitMQ") ?? "amqp://guest:guest@localhost:5672";
                cfg.Host(rmq);
            });
        });

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
