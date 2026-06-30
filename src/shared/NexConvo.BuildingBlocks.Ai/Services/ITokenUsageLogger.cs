using System;
using System.Threading;
using System.Threading.Tasks;

namespace NexConvo.BuildingBlocks.Ai.Services;

public interface ITokenUsageLogger
{
    Task LogUsageAsync(
        Guid tenantId,
        Guid userId,
        string provider,
        string model,
        int promptTokens,
        int completionTokens,
        CancellationToken cancellationToken = default);
}
