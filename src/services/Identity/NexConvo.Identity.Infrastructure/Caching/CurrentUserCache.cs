using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Application.Common;

namespace NexConvo.Identity.Infrastructure.Caching;

/// <summary>
/// <see cref="ICurrentUserCache"/> over <see cref="IDistributedCache"/> (Redis in prod, in-memory
/// in dev when no Redis is configured). JSON-serialized, 30s absolute TTL — short enough that any
/// missed invalidation self-heals quickly, long enough to absorb navigation bursts.
/// </summary>
internal sealed class CurrentUserCache(IDistributedCache cache) : ICurrentUserCache
{
    private static readonly DistributedCacheEntryOptions Options =
        new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) };

    private static string Key(Guid userId) => $"identity:me:{userId:N}";

    public async Task<CurrentUserDto?> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var bytes = await cache.GetAsync(Key(userId), cancellationToken);
        return bytes is null ? null : JsonSerializer.Deserialize<CurrentUserDto>(bytes);
    }

    public Task SetAsync(Guid userId, CurrentUserDto value, CancellationToken cancellationToken) =>
        cache.SetAsync(Key(userId), JsonSerializer.SerializeToUtf8Bytes(value), Options, cancellationToken);

    public Task InvalidateAsync(Guid userId, CancellationToken cancellationToken) =>
        cache.RemoveAsync(Key(userId), cancellationToken);
}
