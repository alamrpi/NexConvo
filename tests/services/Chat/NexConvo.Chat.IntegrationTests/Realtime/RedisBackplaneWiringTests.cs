using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexConvo.Chat.Api.Extensions;
using NexConvo.Chat.Api.Realtime;
using Testcontainers.Redis;

namespace NexConvo.Chat.IntegrationTests.Realtime;

/// <summary>
/// Locks in AddChatRealtime's backplane guard from both sides: a real Redis connection string
/// selects the Redis-backed HubLifetimeManager (cross-pod fan-out — the multi-node message relay
/// itself is Microsoft's RedisHubLifetimeManager, exercised against a real Redis container here),
/// while the empty-string convention used by the ChatApiFactory test host falls back to the
/// in-process default manager instead of failing startup.
/// </summary>
public sealed class RedisBackplaneWiringTests : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder().WithImage("redis:7-alpine").Build();

    public Task InitializeAsync() => _redis.StartAsync();

    public Task DisposeAsync() => _redis.DisposeAsync().AsTask();

    [Fact]
    public void AddChatRealtime_WithRedisConnectionString_UsesRedisHubLifetimeManager()
    {
        using var provider = BuildProvider(_redis.GetConnectionString());

        var manager = provider.GetRequiredService<HubLifetimeManager<ChatHub>>();

        manager.GetType().Name.Should().Contain("Redis");
    }

    [Fact]
    public void AddChatRealtime_WithEmptyRedisConnectionString_UsesDefaultHubLifetimeManager()
    {
        using var provider = BuildProvider(string.Empty);

        var manager = provider.GetRequiredService<HubLifetimeManager<ChatHub>>();

        manager.Should().BeOfType<DefaultHubLifetimeManager<ChatHub>>();
    }

    private static ServiceProvider BuildProvider(string redisConnectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Redis"] = redisConnectionString,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddChatRealtime(configuration);
        return services.BuildServiceProvider();
    }
}
