using FluentAssertions;
using MassTransit;
using MockQueryable.NSubstitute;
using NSubstitute;
using NexConvo.BuildingBlocks.Application.Health;
using NexConvo.BuildingBlocks.Application.Security;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.Integrations.Application.Features.S3Config;
using NexConvo.Integrations.Application.Features.S3Config.Commands;
using NexConvo.Integrations.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NexConvo.Integrations.Application.UnitTests;

public class SaveS3ConfigGateTests
{
    private readonly IIntegrationsDbContext _contextMock = Substitute.For<IIntegrationsDbContext>();
    private readonly ITenantContext _tenantMock = Substitute.For<ITenantContext>();
    private readonly IAesEncryptionService _encryptionServiceMock = Substitute.For<IAesEncryptionService>();
    private readonly IPublishEndpoint _publishEndpointMock = Substitute.For<IPublishEndpoint>();
    private readonly IConnectionTester<S3TestInput> _testerMock = Substitute.For<IConnectionTester<S3TestInput>>();
    private readonly Microsoft.Extensions.Logging.ILogger<SaveS3ConfigCommandHandler> _loggerMock
        = Substitute.For<Microsoft.Extensions.Logging.ILogger<SaveS3ConfigCommandHandler>>();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly SaveS3ConfigCommandHandler _handler;

    public SaveS3ConfigGateTests()
    {
        _tenantMock.TenantId.Returns(_tenantId);
        _encryptionServiceMock.Encrypt(Arg.Any<string>()).Returns(ci => "enc:" + ci.Arg<string>());
        _handler = new SaveS3ConfigCommandHandler(
            _contextMock,
            _tenantMock,
            _encryptionServiceMock,
            _publishEndpointMock,
            _testerMock,
            _loggerMock);
    }

    private SaveS3ConfigCommand Command(bool withNewKeys = true) => new(
        "my-bucket",
        "us-east-1",
        withNewKeys ? "AK" : null,
        withNewKeys ? "SK" : null,
        null,
        null,
        true,
        _actorId);

    private void SetupConfigs(params WorkspaceS3Config[] configs)
    {
        // Build the mock DbSets into locals first — BuildMockDbSet() configures internal substitutes,
        // which would break NSubstitute's last-call tracking if invoked inside .Returns(...).
        var s3Set = configs.ToList().AsQueryable().BuildMockDbSet();
        var auditSet = new List<AuditLog>().AsQueryable().BuildMockDbSet();
        _contextMock.WorkspaceS3Configs.Returns(s3Set);
        _contextMock.AuditLogs.Returns(auditSet);
    }

    [Fact]
    public async Task Save_rejects_with_ConnectionTestFailedException_when_retest_fails()
    {
        SetupConfigs();
        _testerMock.TestAsync(Arg.Any<S3TestInput>(), Arg.Any<CancellationToken>())
            .Returns(ConnectionHealth.Failed("bad credentials", null));

        Func<Task> act = () => _handler.Handle(Command(withNewKeys: true), CancellationToken.None);

        await act.Should().ThrowAsync<ConnectionTestFailedException>();
        _contextMock.WorkspaceS3Configs.DidNotReceive().Add(Arg.Any<WorkspaceS3Config>());
        _contextMock.AuditLogs.DidNotReceive().Add(Arg.Any<AuditLog>());
        await _contextMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _publishEndpointMock.DidNotReceive().Publish(Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_persists_and_applies_health_on_success()
    {
        SetupConfigs();
        _testerMock.TestAsync(Arg.Any<S3TestInput>(), Arg.Any<CancellationToken>())
            .Returns(ConnectionHealth.Healthy("ok", 12));

        await _handler.Handle(Command(withNewKeys: true), CancellationToken.None);

        _contextMock.WorkspaceS3Configs.Received(1).Add(Arg.Is<WorkspaceS3Config>(
            x => x.LastTestStatus == ConnectionStatus.Healthy));
        await _contextMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_skips_retest_when_keys_unchanged()
    {
        var existing = new WorkspaceS3Config(
            _tenantId, "old-bucket", "us-east-1", "ENC_AK", "ENC_SK", null, null, true);
        SetupConfigs(existing);

        await _handler.Handle(Command(withNewKeys: false), CancellationToken.None);

        await _testerMock.DidNotReceive().TestAsync(Arg.Any<S3TestInput>(), Arg.Any<CancellationToken>());
        existing.LastTestStatus.Should().Be(ConnectionStatus.Untested);
        await _contextMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
