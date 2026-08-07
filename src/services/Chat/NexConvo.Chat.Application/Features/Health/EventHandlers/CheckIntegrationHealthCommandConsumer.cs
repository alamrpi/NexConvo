using MassTransit;
using Microsoft.Extensions.Logging;
using NexConvo.Contracts.Messages;

namespace NexConvo.Chat.Application.Features.Health.EventHandlers;

/// <summary>
/// Consumes the fan-out trigger from the Automation scheduler and runs this service's
/// own health sweep (Standard 22d). Carries no payload — Chat enumerates its own
/// active channel connections across all tenants on the owner connection.
/// </summary>
public sealed class CheckIntegrationHealthCommandConsumer(
    IChatHealthSweepService sweepService,
    ILogger<CheckIntegrationHealthCommandConsumer> logger)
    : IConsumer<CheckIntegrationHealthCommand>
{
    public async Task Consume(ConsumeContext<CheckIntegrationHealthCommand> context)
    {
        logger.LogInformation("Chat health sweep starting (CorrelationId {CorrelationId})", context.Message.CorrelationId);

        await sweepService.RunAsync(context.CancellationToken);

        logger.LogInformation("Chat health sweep completed");
    }
}
