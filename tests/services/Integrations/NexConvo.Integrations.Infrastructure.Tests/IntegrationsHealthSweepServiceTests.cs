using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NSubstitute;
using NexConvo.BuildingBlocks.Application.Health;
using NexConvo.BuildingBlocks.Application.Security;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.Contracts.Enums;
using NexConvo.Contracts.Events.Health;
using NexConvo.Integrations.Application;
using NexConvo.Integrations.Application.Features.AiConfig;
using NexConvo.Integrations.Application.Features.S3Config;
using NexConvo.Integrations.Domain.Entities;
using NexConvo.Integrations.Infrastructure.HealthCheck;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NexConvo.Integrations.Infrastructure.Tests;

public class IntegrationsHealthSweepServiceTests
{
    private readonly IIntegrationsDbContext _contextMock = Substitute.For<IIntegrationsDbContext>();
    private readonly IAesEncryptionService _aesMock = Substitute.For<IAesEncryptionService>();
    private readonly IConnectionTester<S3TestInput> _s3TesterMock = Substitute.For<IConnectionTester<S3TestInput>>();
    private readonly IConnectionTester<AiTestInput> _aiTesterMock = Substitute.For<IConnectionTester<AiTestInput>>();
    private readonly IPublishEndpoint _publishEndpointMock = Substitute.For<IPublishEndpoint>();
    private readonly ILogger<IntegrationsHealthSweepService> _loggerMock = Substitute.For<ILogger<IntegrationsHealthSweepService>>();

    private readonly IntegrationsHealthSweepService _sut;

    public IntegrationsHealthSweepServiceTests()
    {
        _aesMock.Decrypt(Arg.Any<string>()).Returns(ci => "dec:" + ci.Arg<string>());
        _s3TesterMock.IntegrationKind.Returns("s3");
        _aiTesterMock.IntegrationKind.Returns("ai");

        // The production sweep builds its own owner-connection DbContext internally; for unit
        // tests we inject a factory that returns our NSubstitute IIntegrationsDbContext instead,
        // so the per-config transition/persist/publish logic is directly testable without a
        // real Postgres connection.
        _sut = new IntegrationsHealthSweepService(
            () => _contextMock,
            _s3TesterMock,
            _aiTesterMock,
            _aesMock,
            _publishEndpointMock,
            _loggerMock);
    }

    private static WorkspaceS3Config MakeS3Config(Guid tenantId, ConnectionStatus previousStatus)
    {
        var config = new WorkspaceS3Config(
            tenantId, "my-bucket", "us-east-1", "ENC_AK", "ENC_SK", null, null, isActive: true);

        if (previousStatus != ConnectionStatus.Untested)
        {
            var health = previousStatus == ConnectionStatus.Healthy
                ? ConnectionHealth.Healthy("ok", 10)
                : ConnectionHealth.Failed("previously bad", 10);
            config.ApplyHealth(health);
        }

        return config;
    }

    private static WorkspaceAiConfig MakeAiConfig(Guid tenantId, ConnectionStatus previousStatus)
    {
        var config = new WorkspaceAiConfig(
            tenantId, AiProviderType.OpenAI, "ENC_KEY", null, "gpt-4o", null, null, isActive: true);

        if (previousStatus != ConnectionStatus.Untested)
        {
            var health = previousStatus == ConnectionStatus.Healthy
                ? ConnectionHealth.Healthy("ok", 10)
                : ConnectionHealth.Failed("previously bad", 10);
            config.ApplyHealth(health);
        }

        return config;
    }

    private void SetupConfigs(WorkspaceS3Config[]? s3Configs = null, WorkspaceAiConfig[]? aiConfigs = null)
    {
        var s3Set = (s3Configs ?? []).ToList().AsQueryable().BuildMockDbSet();
        var aiSet = (aiConfigs ?? []).ToList().AsQueryable().BuildMockDbSet();
        var auditSet = new List<AuditLog>().AsQueryable().BuildMockDbSet();
        _contextMock.WorkspaceS3Configs.Returns(s3Set);
        _contextMock.WorkspaceAiConfigs.Returns(aiSet);
        _contextMock.AuditLogs.Returns(auditSet);
    }

    [Fact]
    public async Task RunAsync_probe_healthy_persists_and_publishes_no_event()
    {
        var tenantId = Guid.NewGuid();
        var config = MakeS3Config(tenantId, ConnectionStatus.Untested);
        SetupConfigs(s3Configs: [config]);

        _s3TesterMock.TestAsync(Arg.Any<S3TestInput>(), Arg.Any<CancellationToken>())
            .Returns(ConnectionHealth.Healthy("reachable", 15));

        await _sut.RunAsync(CancellationToken.None);

        config.LastTestStatus.Should().Be(ConnectionStatus.Healthy);
        await _contextMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _publishEndpointMock.DidNotReceive().Publish(Arg.Any<IntegrationHealthFailedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_previously_healthy_probe_fails_persists_and_publishes_once()
    {
        var tenantId = Guid.NewGuid();
        var config = MakeS3Config(tenantId, ConnectionStatus.Healthy);
        SetupConfigs(s3Configs: [config]);

        _s3TesterMock.TestAsync(Arg.Any<S3TestInput>(), Arg.Any<CancellationToken>())
            .Returns(ConnectionHealth.Failed("sanitized: access denied", 20));

        await _sut.RunAsync(CancellationToken.None);

        config.LastTestStatus.Should().Be(ConnectionStatus.Failed);
        await _contextMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        await _publishEndpointMock.Received(1).Publish(
            Arg.Is<IntegrationHealthFailedEvent>(e =>
                e.TenantId == tenantId &&
                e.IntegrationKind == "s3" &&
                e.ConfigId == config.Id &&
                e.ConfigName == "my-bucket" &&
                e.ErrorMessage == "sanitized: access denied" &&
                e.PreviousStatus == nameof(ConnectionStatus.Healthy)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_previously_failed_probe_fails_persists_but_does_not_republish()
    {
        var tenantId = Guid.NewGuid();
        var config = MakeS3Config(tenantId, ConnectionStatus.Failed);
        SetupConfigs(s3Configs: [config]);

        _s3TesterMock.TestAsync(Arg.Any<S3TestInput>(), Arg.Any<CancellationToken>())
            .Returns(ConnectionHealth.Failed("still bad", 20));

        await _sut.RunAsync(CancellationToken.None);

        config.LastTestStatus.Should().Be(ConnectionStatus.Failed);
        await _contextMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _publishEndpointMock.DidNotReceive().Publish(Arg.Any<IntegrationHealthFailedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_one_config_throws_sweep_continues_to_next_config()
    {
        var tenantId1 = Guid.NewGuid();
        var tenantId2 = Guid.NewGuid();
        var throwingConfig = new WorkspaceS3Config(
            tenantId1, "throwing-bucket", "us-east-1", "ENC_AK", "ENC_SK", null, null, isActive: true);
        var okConfig = new WorkspaceS3Config(
            tenantId2, "ok-bucket", "us-east-1", "ENC_AK", "ENC_SK", null, null, isActive: true);
        SetupConfigs(s3Configs: [throwingConfig, okConfig]);

        _s3TesterMock.TestAsync(
                Arg.Is<S3TestInput>(x => x.BucketName == "throwing-bucket"),
                Arg.Any<CancellationToken>())
            .Returns<Task<ConnectionHealth>>(_ => throw new InvalidOperationException("boom"));

        _s3TesterMock.TestAsync(
                Arg.Is<S3TestInput>(x => x.BucketName == "ok-bucket"),
                Arg.Any<CancellationToken>())
            .Returns(ConnectionHealth.Healthy("ok", 5));

        await _sut.RunAsync(CancellationToken.None);

        okConfig.LastTestStatus.Should().Be(ConnectionStatus.Healthy);
        throwingConfig.LastTestStatus.Should().Be(ConnectionStatus.Untested);
        await _contextMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_ai_config_previously_healthy_probe_fails_publishes_with_provider_as_name()
    {
        var tenantId = Guid.NewGuid();
        var config = MakeAiConfig(tenantId, ConnectionStatus.Healthy);
        SetupConfigs(aiConfigs: [config]);

        _aiTesterMock.TestAsync(Arg.Any<AiTestInput>(), Arg.Any<CancellationToken>())
            .Returns(ConnectionHealth.Failed("sanitized: bad key", 20));

        await _sut.RunAsync(CancellationToken.None);

        config.LastTestStatus.Should().Be(ConnectionStatus.Failed);
        await _publishEndpointMock.Received(1).Publish(
            Arg.Is<IntegrationHealthFailedEvent>(e =>
                e.IntegrationKind == "ai" &&
                e.ConfigName == AiProviderType.OpenAI.ToString() &&
                e.PreviousStatus == nameof(ConnectionStatus.Healthy)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_only_tests_active_configs()
    {
        var tenantId = Guid.NewGuid();
        var inactiveConfig = new WorkspaceS3Config(
            tenantId, "inactive-bucket", "us-east-1", "ENC_AK", "ENC_SK", null, null, isActive: false);
        SetupConfigs(s3Configs: [inactiveConfig]);

        await _sut.RunAsync(CancellationToken.None);

        await _s3TesterMock.DidNotReceive().TestAsync(Arg.Any<S3TestInput>(), Arg.Any<CancellationToken>());
    }
}
