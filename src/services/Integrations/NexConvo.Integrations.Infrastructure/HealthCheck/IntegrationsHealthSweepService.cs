using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Application.Health;
using NexConvo.BuildingBlocks.Application.Security;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.Contracts.Events.Health;
using NexConvo.Integrations.Application;
using NexConvo.Integrations.Application.Features.AiConfig;
using NexConvo.Integrations.Application.Features.Health;
using NexConvo.Integrations.Application.Features.S3Config;
using NexConvo.Integrations.Domain.Entities;
using NexConvo.Integrations.Infrastructure.Persistence;

namespace NexConvo.Integrations.Infrastructure.HealthCheck;

/// <summary>
/// Background health sweep (Standard 22d): re-tests every tenant's active S3 + AI configs and
/// publishes <see cref="IntegrationHealthFailedEvent"/> on a Healthy→Failed transition only.
///
/// Owner-connection enumeration (RLS-free): this sweep must see configs across ALL tenants, not
/// just the caller's own tenant, so it cannot use the request-scoped <see cref="IntegrationsDbContext"/>
/// registered in DI (which has the <c>RlsConnectionInterceptor</c> attached and would only see one
/// tenant's rows). Instead it builds its own <see cref="IntegrationsDbContext"/> directly on the
/// <c>IntegrationsDbMigrator</c> connection string with NO RLS interceptor — mirroring
/// <see cref="IntegrationsDatabaseMigrator"/>, which does the same bare-ctor construction for
/// migrations. The dev "nexconvo" Postgres role is a superuser, so it bypasses RLS transparently.
///
/// PROD CAVEAT: <c>WorkspaceS3Configs</c> has FORCE ROW LEVEL SECURITY, so a non-superuser owner
/// role will NOT bypass RLS in production the way the local dev superuser does. Production needs
/// either a role with BYPASSRLS, or a per-tenant loop that issues `SET LOCAL app.current_tenant_id`
/// before each tenant's query. This is a known gap to close before deploying the sweep to prod.
/// </summary>
public sealed class IntegrationsHealthSweepService : IIntegrationsHealthSweepService
{
    private readonly Func<IIntegrationsDbContext> _contextFactory;
    private readonly IConnectionTester<S3TestInput> _s3Tester;
    private readonly IConnectionTester<AiTestInput> _aiTester;
    private readonly IAesEncryptionService _aes;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<IntegrationsHealthSweepService> _logger;

    /// <summary>Production entry point: builds the owner-connection DbContext from configuration.</summary>
    public IntegrationsHealthSweepService(
        IConfiguration configuration,
        IConnectionTester<S3TestInput> s3Tester,
        IConnectionTester<AiTestInput> aiTester,
        IAesEncryptionService aes,
        IPublishEndpoint publishEndpoint,
        ILogger<IntegrationsHealthSweepService> logger)
        : this(() => CreateOwnerDbContext(configuration), s3Tester, aiTester, aes, publishEndpoint, logger)
    {
    }

    /// <summary>Test entry point: accepts a context factory so the per-config transition/persist/
    /// publish logic is unit-testable against a substitute <see cref="IIntegrationsDbContext"/>
    /// without a real owner connection.</summary>
    public IntegrationsHealthSweepService(
        Func<IIntegrationsDbContext> contextFactory,
        IConnectionTester<S3TestInput> s3Tester,
        IConnectionTester<AiTestInput> aiTester,
        IAesEncryptionService aes,
        IPublishEndpoint publishEndpoint,
        ILogger<IntegrationsHealthSweepService> logger)
    {
        _contextFactory = contextFactory;
        _s3Tester = s3Tester;
        _aiTester = aiTester;
        _aes = aes;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    private static IIntegrationsDbContext CreateOwnerDbContext(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("IntegrationsDbMigrator")
                             ?? configuration.GetConnectionString("IntegrationsDb");

        var optionsBuilder = new DbContextOptionsBuilder<IntegrationsDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        // Bare ctor — no RlsConnectionInterceptor — mirrors IntegrationsDatabaseMigrator so this
        // connection can enumerate every tenant's rows instead of being scoped to one.
        return new IntegrationsDbContext(optionsBuilder.Options);
    }

    public async Task RunAsync(CancellationToken ct)
    {
        // The owner-connection context is created per sweep and owned here — dispose it so the
        // pooled Npgsql connection is released (the production factory new-s a concrete DbContext
        // that DI never disposes; mirrors the `using var` in IntegrationsDatabaseMigrator).
        var context = _contextFactory();
        try
        {
            var s3Configs = await context.WorkspaceS3Configs
                .Where(x => x.IsActive)
                .ToListAsync(ct);

            foreach (var config in s3Configs)
            {
                await SweepS3ConfigAsync(context, config, ct);
            }

            var aiConfigs = await context.WorkspaceAiConfigs
                .Where(x => x.IsActive)
                .ToListAsync(ct);

            foreach (var config in aiConfigs)
            {
                await SweepAiConfigAsync(context, config, ct);
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

    private async Task SweepS3ConfigAsync(IIntegrationsDbContext context, WorkspaceS3Config config, CancellationToken ct)
    {
        try
        {
            var accessKeyId = _aes.Decrypt(config.EncryptedAccessKeyId);
            var secretAccessKey = _aes.Decrypt(config.EncryptedSecretAccessKey);

            var probe = await _s3Tester.TestAsync(
                new S3TestInput(config.BucketName, config.Region, accessKeyId, secretAccessKey, config.CustomEndpoint),
                ct);

            var previousStatus = config.LastTestStatus;
            config.ApplyHealth(probe);
            await context.SaveChangesAsync(ct);

            if (previousStatus == ConnectionStatus.Healthy && !probe.Success)
            {
                await _publishEndpoint.Publish(
                    new IntegrationHealthFailedEvent(
                        config.TenantId,
                        _s3Tester.IntegrationKind,
                        config.Id,
                        config.BucketName,
                        probe.ErrorMessage,
                        nameof(ConnectionStatus.Healthy)),
                    ct);
            }

            _logger.LogInformation(
                "S3 config {ConfigId} health sweep result: {Status}", config.Id, probe.Status);
        }
        catch (Exception ex)
        {
            // Standard 18: one config's failure must never abort the sweep for the rest.
            _logger.LogError(ex, "S3 config {ConfigId} health sweep threw; continuing sweep", config.Id);
        }
    }

    private async Task SweepAiConfigAsync(IIntegrationsDbContext context, WorkspaceAiConfig config, CancellationToken ct)
    {
        try
        {
            var apiKey = _aes.Decrypt(config.EncryptedApiKey);

            var probe = await _aiTester.TestAsync(
                new AiTestInput(config.Provider, apiKey, config.BaseUrl, config.DefaultModel),
                ct);

            var previousStatus = config.LastTestStatus;
            config.ApplyHealth(probe);
            await context.SaveChangesAsync(ct);

            if (previousStatus == ConnectionStatus.Healthy && !probe.Success)
            {
                await _publishEndpoint.Publish(
                    new IntegrationHealthFailedEvent(
                        config.TenantId,
                        _aiTester.IntegrationKind,
                        config.Id,
                        config.Provider.ToString(),
                        probe.ErrorMessage,
                        nameof(ConnectionStatus.Healthy)),
                    ct);
            }

            _logger.LogInformation(
                "AI config {ConfigId} health sweep result: {Status}", config.Id, probe.Status);
        }
        catch (Exception ex)
        {
            // Standard 18: one config's failure must never abort the sweep for the rest.
            _logger.LogError(ex, "AI config {ConfigId} health sweep threw; continuing sweep", config.Id);
        }
    }
}
