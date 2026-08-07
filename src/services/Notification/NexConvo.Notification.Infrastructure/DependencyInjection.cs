using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexConvo.BuildingBlocks.Resilience;
using NexConvo.Notification.Application.Abstractions.Mailing;
using NexConvo.Notification.Application.Abstractions.Recipients;
using NexConvo.Notification.Application.Consumers;
using NexConvo.Notification.Infrastructure.Mailing;
using NexConvo.Notification.Infrastructure.Recipients;

namespace NexConvo.Notification.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Email: platform-default-only options (Standard 13 — Notification is the sole holder of
        // the alert email-provider secret; there is no per-tenant provider here) + Resend HTTP
        // client (Polly) + SMTP sender (Mailpit in dev) + a provider-switch resolver.
        services.Configure<PlatformDefaultEmailOptions>(
            configuration.GetSection(PlatformDefaultEmailOptions.SectionName));
        services.AddHttpClient("resend", client => client.BaseAddress = new Uri("https://api.resend.com/"))
            .AddNexConvoResilience();
        services.AddScoped<ResendEmailSender>();
        services.AddScoped<SmtpEmailSender>();
        services.AddScoped<IEmailSender, EmailSenderResolver>();

        // Identity internal contact lookup: shared-secret guarded, never routed via the public
        // gateway (Standard 12). Resilient (Standard 8) named HttpClient.
        var identityBaseUrl = configuration["Identity:BaseUrl"]
            ?? throw new InvalidOperationException("Configuration 'Identity:BaseUrl' is not configured.");
        services.AddHttpClient("identity-internal", client => client.BaseAddress = new Uri(identityBaseUrl))
            .AddNexConvoResilience();
        services.AddScoped<IHealthAlertRecipientsClient, HealthAlertRecipientsClient>();

        // Consume-only bus: Notification never publishes, it only reacts to health-failed events.
        services.AddMassTransit(x =>
        {
            x.AddConsumers(typeof(IntegrationHealthFailedEventConsumer).Assembly);
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
