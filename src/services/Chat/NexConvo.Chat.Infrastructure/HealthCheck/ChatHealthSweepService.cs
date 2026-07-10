using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Application.Health;
using NexConvo.BuildingBlocks.Application.Security;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.Chat.Application.Common;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Application.Features.Health;
using NexConvo.Chat.Domain.Entities;
using NexConvo.Chat.Infrastructure.Persistence;
using NexConvo.Contracts.Events.Health;

namespace NexConvo.Chat.Infrastructure.HealthCheck;

/// <summary>
/// Background health sweep (Standard 22d): re-tests every tenant's active
/// <see cref="ChannelConnection"/>s and publishes <see cref="IntegrationHealthFailedEvent"/> on
/// a Healthy→Failed transition only.
///
/// Owner-connection enumeration (RLS-free): this sweep must see connections across ALL tenants,
/// not just the caller's own tenant, so it cannot use the request-scoped <see cref="ChatDbContext"/>
/// registered in DI. Instead it builds its own <see cref="ChatDbContext"/> directly on the
/// <c>ChatDbMigrator</c> connection string with a <see cref="NullTenantContext"/> (which leaves the
/// RLS interceptor inert — it never emits set_config without a tenant)
/// — mirroring <see cref="ChatDatabaseMigrator"/>, which does the same bare-ctor construction for
/// migrations. The dev "nexconvo" Postgres role is a superuser, so it bypasses RLS transparently.
///
/// PROD CAVEAT: same as the Integrations sweep — a non-superuser owner role will NOT bypass RLS
/// in production. This is a known gap to close before deploying the sweep to prod.
/// </summary>
public sealed class ChatHealthSweepService : IChatHealthSweepService
{
    private readonly Func<IChatDbContext> _contextFactory;
    private readonly IConnectionTester<ChannelTestInput> _channelTester;
    private readonly IAesEncryptionService _aes;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<ChatHealthSweepService> _logger;

    /// <summary>Production entry point: builds the owner-connection DbContext from configuration.</summary>
    public ChatHealthSweepService(
        IConfiguration configuration,
        IConnectionTester<ChannelTestInput> channelTester,
        IAesEncryptionService aes,
        IPublishEndpoint publishEndpoint,
        ILogger<ChatHealthSweepService> logger)
        : this(() => CreateOwnerDbContext(configuration), channelTester, aes, publishEndpoint, logger)
    {
    }

    /// <summary>Test entry point: accepts a context factory so the per-connection transition/
    /// persist/publish logic is unit-testable against a substitute <see cref="IChatDbContext"/>
    /// without a real owner connection.</summary>
    public ChatHealthSweepService(
        Func<IChatDbContext> contextFactory,
        IConnectionTester<ChannelTestInput> channelTester,
        IAesEncryptionService aes,
        IPublishEndpoint publishEndpoint,
        ILogger<ChatHealthSweepService> logger)
    {
        _contextFactory = contextFactory;
        _channelTester = channelTester;
        _aes = aes;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    private static IChatDbContext CreateOwnerDbContext(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ChatDbMigrator")
                             ?? configuration.GetConnectionString("ChatDb");

        var optionsBuilder = new DbContextOptionsBuilder<ChatDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        // Bare ctor with a NullTenantContext — the RlsConnectionInterceptor is attached by
        // ChatDbContext.OnConfiguring but stays inert because NullTenantContext.HasTenant is false
        // (so it never emits set_config) — mirrors ChatDatabaseMigrator, letting this connection
        // enumerate every tenant's rows instead of being scoped to one.
        return new ChatDbContext(optionsBuilder.Options, new NullTenantContext());
    }

    public async Task RunAsync(CancellationToken ct)
    {
        // The owner-connection context is created per sweep and owned here — dispose it so the
        // pooled Npgsql connection is released (the production factory new-s a concrete DbContext
        // that DI never disposes; mirrors the `using var` in ChatDatabaseMigrator).
        var context = _contextFactory();
        try
        {
            var connections = await context.ChannelConnections
                .Where(c => c.IsActive)
                .ToListAsync(ct);

            foreach (var connection in connections)
            {
                await SweepConnectionAsync(context, connection, ct);
            }
        }
        finally
        {
            if (context is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            else if (context is IDisposable disposable)
                disposable.Dispose();
        }
    }

    private async Task SweepConnectionAsync(IChatDbContext context, ChannelConnection connection, CancellationToken ct)
    {
        try
        {
            var accessToken = _aes.Decrypt(connection.EncryptedAccessToken);

            var probe = await _channelTester.TestAsync(
                new ChannelTestInput(connection.Channel, accessToken, connection.ExternalAccountId),
                ct);

            var previousStatus = connection.LastTestStatus;
            connection.ApplyHealth(probe);
            await context.SaveChangesAsync(ct);

            if (previousStatus == ConnectionStatus.Healthy && !probe.Success)
            {
                await _publishEndpoint.Publish(
                    new IntegrationHealthFailedEvent(
                        connection.TenantId,
                        _channelTester.IntegrationKind,
                        connection.Id,
                        $"{connection.Channel}:{connection.ExternalAccountId}",
                        probe.ErrorMessage,
                        nameof(ConnectionStatus.Healthy)),
                    ct);
            }

            _logger.LogInformation(
                "Channel connection {ConnectionId} health sweep result: {Status}", connection.Id, probe.Status);
        }
        catch (Exception ex)
        {
            // Standard 18: one connection's failure must never abort the sweep for the rest.
            _logger.LogError(ex, "Channel connection {ConnectionId} health sweep threw; continuing sweep", connection.Id);
        }
    }
}
