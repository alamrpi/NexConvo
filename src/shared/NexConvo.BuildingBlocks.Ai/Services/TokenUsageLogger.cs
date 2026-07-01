using MassTransit;
using NexConvo.Contracts.Events.Integrations;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NexConvo.BuildingBlocks.Ai.Services;

public class TokenUsageLogger : ITokenUsageLogger
{
    private readonly IPublishEndpoint _publishEndpoint;

    public TokenUsageLogger(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public async Task LogUsageAsync(
        Guid tenantId, 
        Guid userId, 
        string provider, 
        string model, 
        int promptTokens, 
        int completionTokens, 
        CancellationToken cancellationToken = default)
    {
        var usageEvent = new AiTokenUsageReportedEvent(
            tenantId,
            userId,
            provider,
            model,
            promptTokens,
            completionTokens,
            DateTimeOffset.UtcNow);

        await _publishEndpoint.Publish(usageEvent, cancellationToken);
    }
}
