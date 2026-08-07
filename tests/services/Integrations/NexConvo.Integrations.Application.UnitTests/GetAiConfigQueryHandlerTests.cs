using FluentAssertions;
using MockQueryable.NSubstitute;
using NSubstitute;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.Contracts.Enums;
using NexConvo.Integrations.Application.Features.AiConfig.Queries;
using NexConvo.Integrations.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NexConvo.Integrations.Application.UnitTests;

public class GetAiConfigQueryHandlerTests
{
    private readonly IIntegrationsDbContext _contextMock = Substitute.For<IIntegrationsDbContext>();
    private readonly ITenantContext _tenantMock = Substitute.For<ITenantContext>();
    private readonly Microsoft.Extensions.Logging.ILogger<GetAiConfigQueryHandler> _loggerMock
        = Substitute.For<Microsoft.Extensions.Logging.ILogger<GetAiConfigQueryHandler>>();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly GetAiConfigQueryHandler _handler;

    public GetAiConfigQueryHandlerTests()
    {
        _tenantMock.TenantId.Returns(_tenantId);
        _handler = new GetAiConfigQueryHandler(_contextMock, _tenantMock, _loggerMock);
    }

    private void SetupConfigs(params WorkspaceAiConfig[] configs)
    {
        var aiSet = configs.ToList().AsQueryable().BuildMockDbSet();
        _contextMock.WorkspaceAiConfigs.Returns(aiSet);
    }

    [Fact]
    public async Task Handle_maps_connection_health_fields_onto_dto()
    {
        var config = new WorkspaceAiConfig(
            _tenantId, AiProviderType.OpenAI, "ENC_KEY", null, "gpt-4o", null, null, true);
        config.ApplyHealth(ConnectionHealth.Healthy("ok", 42));
        SetupConfigs(config);

        var result = await _handler.Handle(new GetAiConfigQuery(), CancellationToken.None);

        result.Should().ContainSingle();
        var dto = result.Single();
        dto.LastTestStatus.Should().Be(ConnectionStatus.Healthy);
        dto.LastTestedAt.Should().Be(config.LastTestedAt);
        dto.LastTestError.Should().BeNull();
        dto.LastTestLatencyMs.Should().Be(42);
    }

    [Fact]
    public async Task Handle_returns_empty_list_when_no_configs_exist()
    {
        SetupConfigs();

        var result = await _handler.Handle(new GetAiConfigQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }
}
