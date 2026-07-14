using NexConvo.Chat.Api.Realtime;
using NexConvo.Chat.Application.Rag;
using StackExchange.Redis;

namespace NexConvo.Chat.Api.Extensions;

public static class RealtimeServiceCollectionExtensions
{
    /// <summary>
    /// SignalR + the Redis backplane (so group broadcasts reach clients connected to other pods)
    /// + the SignalR implementation of the channel-neutral reply stream seam.
    ///
    /// The backplane reuses the same "Redis" connection string as the distributed cache. An
    /// empty/missing value skips the backplane (single-node delivery) rather than failing startup
    /// — that is the integration-test convention (ConnectionStrings__Redis="") — but it is logged
    /// as a warning at registration time so a production misconfiguration is loud in Seq.
    /// </summary>
    public static IServiceCollection AddChatRealtime(this IServiceCollection services, IConfiguration configuration)
    {
        var signalR = services.AddSignalR();

        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            signalR.AddStackExchangeRedis(redisConnectionString, options =>
                options.Configuration.ChannelPrefix = RedisChannel.Literal("nexconvo:chat:signalr"));
        }
        else
        {
            Serilog.Log.Warning("SignalR Redis backplane disabled — no Redis connection string; delivery is single-node only");
        }

        // Singleton: IHubContext<ChatHub> is a root singleton, and the sink is stateless. This also
        // lets MassTransit consumer scopes (where ReplyOrchestrator runs) resolve it directly.
        services.AddSingleton<IReplyStreamSink, SignalRReplyStreamSink>();

        return services;
    }
}
