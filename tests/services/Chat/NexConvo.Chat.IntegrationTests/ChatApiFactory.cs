using System.Security.Claims;
using System.Text;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NexConvo.BuildingBlocks.Ai.Models;
using NexConvo.BuildingBlocks.Ai.Services;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Domain.Entities;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Chat.Domain.ValueObjects;
using NexConvo.Chat.Infrastructure.Persistence;
using NexConvo.Contracts.Enums;
using NSubstitute;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace NexConvo.Chat.IntegrationTests;

/// <summary>
/// Boots the Chat API against throwaway Postgres and RabbitMQ containers, mirroring
/// KnowledgeApiFactory's Postgres-only setup. A REAL RabbitMQ (not MassTransit's in-memory test
/// harness) is used deliberately: AddChatInfrastructure's AddMassTransit(...UsingRabbitMq...) call
/// is production code and is not test-aware, so swapping to the in-memory harness would require
/// either a second, conflicting AddMassTransit registration or branching production DI on the
/// environment — both worse than paying for a real broker container, which also gives the most
/// faithful exercise of the actual outbox/inbox wiring this test suite exists to verify. The
/// Knowledge gRPC client and AI provider factory are still swapped for deterministic doubles —
/// this project owns Chat's side of the outbox/audit/RLS/idempotency contract, not Knowledge's
/// retrieval or a real LLM.
/// </summary>
public sealed class ChatApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string ServicePassword = "svc_pw";

    /// <summary>
    /// Symmetric key for test-issued JWTs. Production validates via the Identity authority's JWKS;
    /// tests have no Identity service, so ConfigureWebHost swaps validation to this key while
    /// keeping the rest of the JwtBearer pipeline (including the /hubs access_token query-string
    /// hook from Program.cs) fully real.
    /// </summary>
    private static readonly SymmetricSecurityKey TestSigningKey =
        new(Encoding.UTF8.GetBytes("nexconvo-chat-integration-test-signing-key-48ch!"));

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("nexconvo_chat")
        .WithUsername("nexconvo")
        .WithPassword("superuser_pw")
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder()
        .WithImage("rabbitmq:3-management")
        .Build();

    public IKnowledgeRetrievalClient KnowledgeMock { get; } = Substitute.For<IKnowledgeRetrievalClient>();

    public IAiProviderFactory AiProviderFactoryMock { get; } = Substitute.For<IAiProviderFactory>();

    private string ServiceConnectionString =>
        $"Host=localhost;Port={_postgres.GetMappedPublicPort(5432)};Database=nexconvo_chat;" +
        $"Username=nexconvo_service;Password={ServicePassword}";

    private string SuperuserConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync());

        await _postgres.ExecScriptAsync($"CREATE ROLE nexconvo_service LOGIN PASSWORD '{ServicePassword}';");

        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseNpgsql(SuperuserConnectionString)
            .Options;
        await using var context = new ChatDbContext(options, new NullTenantContext());
        await context.Database.MigrateAsync();

        // WebApplicationFactory's ConfigureWebHost -> ConfigureAppConfiguration hook does not take
        // effect for this minimal-hosting-model app's HostFactoryResolver bootstrap path (same
        // caveat KnowledgeApiFactory documents), so test-only settings go via environment
        // variables, which Program.cs's WebApplication.CreateBuilder always reads.
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "IntegrationTest");
        Environment.SetEnvironmentVariable("ConnectionStrings__ChatDb", ServiceConnectionString);
        Environment.SetEnvironmentVariable("ConnectionStrings__ChatDbMigrator", SuperuserConnectionString);
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", "");
        Environment.SetEnvironmentVariable("ConnectionStrings__RabbitMQ", _rabbitMq.GetConnectionString());
        Environment.SetEnvironmentVariable("Knowledge__GrpcAddress", "http://localhost:0");
        Environment.SetEnvironmentVariable("Internal__ApiKey", "test-internal-api-key");
        Environment.SetEnvironmentVariable("SecuritySettings__AiMasterKey", "integration-test-aes-key-32chars");

        ConfigureDefaultDoubles();

        // WebApplicationFactory only creates the DI container lazily; hosted services (including
        // MassTransit's bus, whose consumers this whole test project exercises) don't actually
        // start until the test server itself starts. CreateClient() forces that startup.
        using var _ = CreateClient();

        // RabbitMQ's AMQP listener can accept the container's health probe before it is actually
        // ready to accept client connections, so MassTransit's first connection attempt(s) can
        // fail and retry with backoff even though the container "started" successfully. Wait for
        // the bus to report Healthy before returning, so no individual test's own polling window
        // has to additionally absorb bus-startup latency.
        using var scope = Services.CreateScope();
        var busControl = scope.ServiceProvider.GetRequiredService<IBusControl>();
        for (var attempt = 0; attempt < 30; attempt++)
        {
            var health = busControl.CheckHealth();
            if (health.Status == BusHealthStatus.Healthy) break;
            await Task.Delay(1000);
        }
    }

    private void ConfigureDefaultDoubles()
    {
        KnowledgeMock.SearchAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns([new KnowledgeChunkMatch("chunk-1", "doc-1", "Refunds are processed within 5 business days.", 0.9)]);

        var aiProvider = Substitute.For<IAiProviderService>();
        aiProvider.GenerateStreamAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(StreamOf("Refunds take 5 business days. [1]"));
        AiProviderFactoryMock.GetProvider(Arg.Any<AiProviderType>()).Returns(aiProvider);
    }

    private static async IAsyncEnumerable<AiStreamChunk> StreamOf(params string[] chunks)
    {
        foreach (var c in chunks)
        {
            yield return new AiStreamChunk(c, Reason: null, PromptTokens: null, CompletionTokens: null);
            await Task.Yield();
        }
    }

    /// <summary>A superuser context for direct test seeding/asserting (bypasses RLS).</summary>
    public ChatDbContext CreateSeedContext(Guid tenantId) => CreateDbContextFor(tenantId, SuperuserConnectionString);

    /// <summary>The RLS-enforced context the service itself would use, pinned to one tenant.</summary>
    public ChatDbContext CreateDbContext(Guid tenantId) => CreateDbContextFor(tenantId, ServiceConnectionString);

    private static ChatDbContext CreateDbContextFor(Guid tenantId, string connectionString)
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new ChatDbContext(options, new FixedTenantContext(tenantId));
    }

    public async Task SeedWorkspaceChatSettingsAsync(Guid tenantId, double handoffThreshold)
    {
        await using var db = CreateSeedContext(tenantId);
        db.WorkspaceChatSettings.Add(new WorkspaceChatSettings(
            tenantId, AiProviderType.OpenAI, "gpt-4o-mini", "[]", null, handoffThreshold, false,
            SentimentSensitivity.Medium, "[]", 3, PiiMaskingLevel.Off, null));
        await db.SaveChangesAsync();

        await CacheAiConfigAsync(tenantId);
    }

    private async Task CacheAiConfigAsync(Guid tenantId)
    {
        using var scope = Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();

        // ReplyOrchestrator calls the real IAesEncryptionService.Decrypt on this value (that
        // service isn't mocked — only the gRPC/LLM boundary is), so it must be real ciphertext,
        // not a placeholder string, or Decrypt throws FormatException on invalid Base64.
        var aes = scope.ServiceProvider.GetRequiredService<NexConvo.BuildingBlocks.Application.Security.IAesEncryptionService>();
        var encryptedApiKey = aes.Encrypt("test-api-key");

        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            TenantId = tenantId,
            Provider = "OpenAI",
            EncryptedApiKey = encryptedApiKey,
            BaseUrl = (string?)null,
            DefaultModel = "gpt-4o-mini",
            SystemPrompt = (string?)null,
            Parameters = (string?)null,
            IsActive = true,
        });
        await cache.SetStringAsync($"AiConfig:{tenantId}", payload);
    }

    /// <summary>
    /// Seeds an open AiHandling conversation so hub tests can join it and so a later
    /// MessageReceivedIntegrationEvent with the same Channel + ExternalSenderId appends to this
    /// exact conversation (MessageReceivedConsumer matches on that channel-thread identity).
    /// </summary>
    public async Task<Guid> SeedConversationAsync(Guid tenantId, string externalSenderId)
    {
        await using var db = CreateSeedContext(tenantId);
        var conversation = Conversation.StartAiHandling(
            tenantId, new ChannelIdentity(LeadSourceChannel.WhatsApp, externalSenderId), contactId: null);
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync();
        return conversation.Id;
    }

    /// <summary>
    /// Seeds everything WidgetHub's anonymous connect path needs: WorkspaceChatSettings (for the
    /// cached AI config + a deterministic WidgetToken) and an active Web ChannelConnection (required
    /// by resolve_widget_tenant's SECURITY DEFINER gate — see WidgetTokenResolutionAndColumnFix).
    /// Returns the WidgetToken to use as the hub's <c>?token=</c> query parameter.
    /// </summary>
    public async Task<Guid> SeedWidgetTenantAsync(Guid tenantId, double handoffThreshold = 0.3)
    {
        await using (var db = CreateSeedContext(tenantId))
        {
            db.WorkspaceChatSettings.Add(new WorkspaceChatSettings(
                tenantId, AiProviderType.OpenAI, "gpt-4o-mini", "[]", null, handoffThreshold, false,
                SentimentSensitivity.Medium, "[]", 3, PiiMaskingLevel.Off, null));
            db.ChannelConnections.Add(new ChannelConnection(
                tenantId, ChatChannel.Web, externalAccountId: "web", accountName: "Web",
                encryptedAccessToken: "enc", isActive: true));
            await db.SaveChangesAsync();
        }

        await CacheAiConfigAsync(tenantId);

        // WidgetToken is assigned at random by the entity's default; read back the actual value
        // rather than pinning it via a raw UPDATE (unlike WidgetTenantResolverTests, this factory
        // has no need for a specific known token — any valid one works for the hub connection).
        await using var read = CreateSeedContext(tenantId);
        var widgetToken = await read.WorkspaceChatSettings
            .Where(s => s.TenantId == tenantId)
            .Select(s => s.WidgetToken)
            .SingleAsync();
        return widgetToken;
    }

    /// <summary>Mints an HS256 JWT with the same claim names Identity's JwtTokenIssuer uses.</summary>
    public static string CreateAccessToken(Guid tenantId, Guid userId, params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new("sub", userId.ToString()),
            new("tenant_id", tenantId.ToString()),
        };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(TestSigningKey, SecurityAlgorithms.HmacSha256),
        });
    }

    /// <summary>
    /// A hub connection through the in-memory TestServer. The token rides the access_token query
    /// parameter (not AccessTokenProvider) to deliberately exercise Program.cs's OnMessageReceived
    /// hook — the same path a browser WebSocket handshake uses. LongPolling is pinned because the
    /// WebSocket transport bypasses HttpMessageHandlerFactory and can't reach the TestServer.
    /// </summary>
    public HubConnection CreateHubConnection(string? accessToken)
    {
        var url = "http://localhost/hubs/chat";
        if (!string.IsNullOrEmpty(accessToken))
        {
            url += $"?access_token={Uri.EscapeDataString(accessToken)}";
        }

        return new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                options.HttpMessageHandlerFactory = _ => Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
    }

    /// <summary>
    /// A WidgetHub connection through the in-memory TestServer. WidgetHub is [AllowAnonymous] and
    /// resolves its tenant from the <c>?token=</c> query parameter (the tenant's WidgetToken) rather
    /// than a JWT — see WidgetHub.GetWidgetToken. LongPolling for the same TestServer reason as
    /// CreateHubConnection above.
    /// </summary>
    public HubConnection CreateWidgetHubConnection(Guid widgetToken)
    {
        var url = $"http://localhost/hubs/widget?token={Uri.EscapeDataString(widgetToken.ToString())}";

        return new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                options.HttpMessageHandlerFactory = _ => Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
    }

    /// <summary>
    /// Publishes directly via IBus (not the scoped IPublishEndpoint) so the call goes straight to
    /// the transport instead of being buffered by the EF outbox — the outbox only intercepts
    /// publishes made inside a unit of work that later calls ChatDbContext.SaveChangesAsync, which
    /// nothing in this test helper does. Production callers (MessageReceivedConsumer,
    /// ReplyOrchestrator) always publish from inside such a unit of work, so they get outbox
    /// behavior naturally; this helper simulates an external event source publishing in.
    /// </summary>
    public async Task PublishAsync<T>(T message, Guid? messageId = null) where T : class
    {
        var bus = Services.GetRequiredService<IBus>();
        await bus.Publish(message, ctx =>
        {
            if (messageId is { } id) ctx.MessageId = id;
        });
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(KnowledgeMock);
            services.AddSingleton(AiProviderFactoryMock);

            // Production JwtBearer validates against the Identity authority (OIDC discovery +
            // JWKS), which isn't running here. This Configure runs AFTER Program.cs's own
            // AddJwtBearer callback (registration order) but BEFORE the framework's
            // JwtBearerPostConfigureOptions — which would otherwise reject the http:// authority
            // outright (RequireHttpsMetadata is true outside Development). It mutates the options
            // in place, so Program.cs's Events (the /hubs access_token hook) survive; only the
            // validation source is swapped to the local symmetric test key.
            services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = null;
                options.ConfigurationManager = null;
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = TestSigningKey,
                };
            });

            // Real Redis isn't running for this test host. AddChatInfrastructure already
            // registered a Redis-backed IDistributedCache (via AddStackExchangeRedisCache, which
            // TryAdd-registers and so does AddDistributedMemoryCache) — remove that registration
            // explicitly before adding the in-memory replacement, otherwise the first-registered
            // Redis implementation wins and every cache read/write throws.
            services.RemoveAll<IDistributedCache>();
            services.AddDistributedMemoryCache();
        });
    }

    public new async Task DisposeAsync()
    {
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _rabbitMq.DisposeAsync().AsTask());
        await base.DisposeAsync();
    }
}

/// <summary>A no-op ITenantContext for fixture-driven migrations that have no HTTP context.</summary>
internal sealed class NullTenantContext : ITenantContext
{
    public bool HasTenant => false;
    public Guid TenantId => throw new InvalidOperationException(
        "No tenant context is available outside an HTTP request scope.");
}

/// <summary>An ITenantContext pinned to one tenant, letting the RLS interceptor scope the connection.</summary>
internal sealed class FixedTenantContext(Guid tenantId) : ITenantContext
{
    public bool HasTenant => true;
    public Guid TenantId => tenantId;
}

[CollectionDefinition("ChatApi")]
public sealed class ChatApiTestCollectionDefinition : ICollectionFixture<ChatApiFactory>;
