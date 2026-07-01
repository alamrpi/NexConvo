using MassTransit;
using Microsoft.Extensions.Caching.Distributed;
using NexConvo.Contracts.Events.Integrations;
using System.Text.Json;
using System.Threading.Tasks;

namespace NexConvo.Chat.Application.Features.AiConfig.EventHandlers;

public class AiConfigUpdatedEventConsumer : IConsumer<AiConfigUpdatedEvent>
{
    private readonly IDistributedCache _cache;

    public AiConfigUpdatedEventConsumer(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task Consume(ConsumeContext<AiConfigUpdatedEvent> context)
    {
        var message = context.Message;
        
        // Cache key specific to the tenant
        var cacheKey = $"AiConfig:{message.TenantId}";

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
            // If an active config is marked inactive (e.g. they turned it off or switched providers), 
            // we should probably check if it's the currently cached one and remove it.
            // For simplicity, if we receive an IsActive = false, we might just delete it if it matches the provider,
            // but usually the active one overrides it.
            var existing = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(existing))
            {
                var existingConfig = JsonSerializer.Deserialize<AiConfigUpdatedEvent>(existing);
                if (existingConfig?.Provider == message.Provider)
                {
                    await _cache.RemoveAsync(cacheKey);
                }
            }
        }
    }
}
