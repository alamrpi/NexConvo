using FluentAssertions;
using MockQueryable.NSubstitute;
using NSubstitute;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.Integrations.Application.Features.S3Config.Queries;
using NexConvo.Integrations.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NexConvo.Integrations.Application.UnitTests;

public class GetS3ConfigQueryHandlerTests
{
    private readonly IIntegrationsDbContext _contextMock = Substitute.For<IIntegrationsDbContext>();
    private readonly ITenantContext _tenantMock = Substitute.For<ITenantContext>();
    private readonly Microsoft.Extensions.Logging.ILogger<GetS3ConfigQueryHandler> _loggerMock
        = Substitute.For<Microsoft.Extensions.Logging.ILogger<GetS3ConfigQueryHandler>>();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly GetS3ConfigQueryHandler _handler;

    public GetS3ConfigQueryHandlerTests()
    {
        _tenantMock.TenantId.Returns(_tenantId);
        _handler = new GetS3ConfigQueryHandler(_contextMock, _tenantMock, _loggerMock);
    }

    private void SetupConfigs(params WorkspaceS3Config[] configs)
    {
        var s3Set = configs.ToList().AsQueryable().BuildMockDbSet();
        _contextMock.WorkspaceS3Configs.Returns(s3Set);
    }

    [Fact]
    public async Task Handle_maps_connection_health_fields_onto_dto()
    {
        var config = new WorkspaceS3Config(
            _tenantId, "my-bucket", "us-east-1", "ENC_AK", "ENC_SK", null, null, true);
        config.ApplyHealth(ConnectionHealth.Healthy("Bucket reachable", 42));
        SetupConfigs(config);

        var result = await _handler.Handle(new GetS3ConfigQuery(), CancellationToken.None);

        result.Should().NotBeNull();
        result!.LastTestStatus.Should().Be(ConnectionStatus.Healthy);
        result.LastTestedAt.Should().Be(config.LastTestedAt);
        result.LastTestError.Should().BeNull();
        result.LastTestLatencyMs.Should().Be(42);
    }

    [Fact]
    public async Task Handle_returns_null_when_no_config_exists()
    {
        SetupConfigs();

        var result = await _handler.Handle(new GetS3ConfigQuery(), CancellationToken.None);

        result.Should().BeNull();
    }
}
