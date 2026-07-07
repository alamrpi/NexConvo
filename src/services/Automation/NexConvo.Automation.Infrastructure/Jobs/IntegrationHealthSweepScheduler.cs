using MassTransit;
using Microsoft.Extensions.Logging;
using NexConvo.Contracts.Messages;

namespace NexConvo.Automation.Infrastructure.Jobs;

/// <summary>
/// Trigger-only Hangfire job: fans out a single <see cref="CheckIntegrationHealthCommand"/> onto
/// RabbitMQ on a recurring cron schedule. Automation holds no credentials and no per-tenant config
/// state — each owning service (Chat, Integrations, ...) consumes the command and re-tests its own
/// active configs.
/// </summary>
public sealed class IntegrationHealthSweepScheduler(
    IPublishEndpoint publishEndpoint,
    ILogger<IntegrationHealthSweepScheduler> logger)
{
    public async Task Trigger()
    {
        logger.LogInformation("Publishing scheduled integration health sweep trigger");

        await publishEndpoint.Publish(new CheckIntegrationHealthCommand(), CancellationToken.None);

        logger.LogInformation("Integration health sweep trigger published");
    }
}
