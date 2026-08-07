using FluentAssertions;
using MassTransit;
using MockQueryable.NSubstitute;
using NSubstitute;
using NexConvo.BuildingBlocks.Application.Health;
using NexConvo.BuildingBlocks.Application.Security;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.Contracts.Enums;
using NexConvo.Integrations.Application.Features.AiConfig;
using NexConvo.Integrations.Application.Features.AiConfig.Commands;
using NexConvo.Integrations.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NexConvo.Integrations.Application.UnitTests;

public class SaveAiConfigGateTests
{
    private readonly IIntegrationsDbContext _contextMock = Substitute.For<IIntegrationsDbContext>();
    private readonly ITenantContext _tenantMock = Substitute.For<ITenantContext>();
    private readonly IAesEncryptionService _encryptionServiceMock = Substitute.For<IAesEncryptionService>();
    private readonly IPublishEndpoint _publishEndpointMock = Substitute.For<IPublishEndpoint>();
    private readonly IConnectionTester<AiTestInput> _testerMock = Substitute.For<IConnectionTester<AiTestInput>>();
    private readonly Microsoft.Extensions.Logging.ILogger<SaveAiConfigCommandHandler> _loggerMock
        = Substitute.For<Microsoft.Extensions.Logging.ILogger<SaveAiConfigCommandHandler>>();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly SaveAiConfigCommandHandler _handler;

    public SaveAiConfigGateTests()
    {
        _tenantMock.TenantId.Returns(_tenantId);
        _encryptionServiceMock.Encrypt(Arg.Any<string>()).Returns(ci => "enc:" + ci.Arg<string>());
        _handler = new SaveAiConfigCommandHandler(
            _contextMock,
            _tenantMock,
            _encryptionServiceMock,
            _publishEndpointMock,
            _testerMock,
            _loggerMock);
    }

    private SaveAiConfigCommand Command(bool withNewKey = true) => new(
        AiProviderType.OpenAI,
        withNewKey ? "sk-test" : null,
        null,
        "gpt-4o",
        null,
        true,
        _actorId);

    private void SetupConfigs(params WorkspaceAiConfig[] configs)
    {
        // Build the mock DbSets into locals first — BuildMockDbSet() configures internal substitutes,
        // which would break NSubstitute's last-call tracking if invoked inside .Returns(...).
        var aiSet = configs.ToList().AsQueryable().BuildMockDbSet();
        var auditSet = new List<AuditLog>().AsQueryable().BuildMockDbSet();
        _contextMock.WorkspaceAiConfigs.Returns(aiSet);
        _contextMock.AuditLogs.Returns(auditSet);
    }

    [Fact]
    public async Task Save_rejects_with_ConnectionTestFailedException_when_retest_fails()
    {
        SetupConfigs();
        _testerMock.TestAsync(Arg.Any<AiTestInput>(), Arg.Any<CancellationToken>())
            .Returns(ConnectionHealth.Failed("bad api key", null));

        Func<Task> act = () => _handler.Handle(Command(withNewKey: true), CancellationToken.None);

        await act.Should().ThrowAsync<ConnectionTestFailedException>();
        _contextMock.WorkspaceAiConfigs.DidNotReceive().Add(Arg.Any<WorkspaceAiConfig>());
        _contextMock.AuditLogs.DidNotReceive().Add(Arg.Any<AuditLog>());
        await _contextMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _publishEndpointMock.DidNotReceive().Publish(Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_persists_and_applies_health_on_success()
    {
        SetupConfigs();
        _testerMock.TestAsync(Arg.Any<AiTestInput>(), Arg.Any<CancellationToken>())
            .Returns(ConnectionHealth.Healthy("ok", 12));

        await _handler.Handle(Command(withNewKey: true), CancellationToken.None);

        _contextMock.WorkspaceAiConfigs.Received(1).Add(Arg.Is<WorkspaceAiConfig>(
            x => x.LastTestStatus == ConnectionStatus.Healthy));
        await _contextMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_skips_retest_when_key_unchanged()
    {
        var existing = new WorkspaceAiConfig(
            _tenantId, AiProviderType.OpenAI, "ENC_KEY", null, "gpt-4o", null, null, true);
        SetupConfigs(existing);

        await _handler.Handle(Command(withNewKey: false), CancellationToken.None);

        await _testerMock.DidNotReceive().TestAsync(Arg.Any<AiTestInput>(), Arg.Any<CancellationToken>());
        existing.LastTestStatus.Should().Be(ConnectionStatus.Untested);
        await _contextMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
