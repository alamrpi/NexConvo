using MassTransit;
using Microsoft.Extensions.Caching.Distributed;
using NexConvo.Contracts.Events.Integrations;
using System.Text.Json;
using System.Threading.Tasks;

namespace NexConvo.Knowledge.Application.Features.S3Config.EventHandlers;

/// <summary>
/// Caches the workspace S3 config (Integrations service is the owner) into Redis so Knowledge's
/// ingestion pipeline can resolve bucket/credentials without a cross-service call per request.
/// Mirrors NexConvo.Chat.Application's AiConfigUpdatedEventConsumer exactly. Only the ENCRYPTED
/// access key/secret are cached (Standard 13/15) — decryption happens at use-time in
/// S3StorageService, never here.
/// </summary>
public class S3ConfigUpdatedEventConsumer : IConsumer<S3ConfigUpdatedEvent>
{
    private readonly IDistributedCache _cache;

    public S3ConfigUpdatedEventConsumer(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task Consume(ConsumeContext<S3ConfigUpdatedEvent> context)
    {
        var message = context.Message;

        // Cache key specific to the tenant
        var cacheKey = $"S3Config:{message.TenantId}";

        if (message.IsActive)
        {
            var serializedConfig = JsonSerializer.Serialize(message);

            // Bounded TTL so a missed invalidation can't leave a stale (key-bearing) entry in Redis
            // indefinitely; a live save refreshes it well before expiry.
            await _cache.SetStringAsync(cacheKey, serializedConfig, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
            });
        }
        else
        {
            // Config was deactivated — remove it so callers hit the "no S3 configuration cached"
            // guard immediately instead of relying on the TTL to expire it.
            await _cache.RemoveAsync(cacheKey);
        }
    }
}
