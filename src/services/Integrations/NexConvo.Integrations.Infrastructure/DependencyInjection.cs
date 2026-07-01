using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MassTransit;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.Integrations.Application;
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

        return services;
    }
}
