using FluentAssertions;
using MassTransit;
using MockQueryable.NSubstitute;
using NSubstitute;
using NexConvo.BuildingBlocks.Application.Health;
using NexConvo.BuildingBlocks.Application.Security;
using NexConvo.BuildingBlocks.Domain;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.Contracts.Enums;
using NexConvo.Contracts.Events.Integrations;
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

public class SaveAiConfigCommandHandlerTests
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

    public SaveAiConfigCommandHandlerTests()
    {
        _tenantMock.TenantId.Returns(_tenantId);
        _testerMock.TestAsync(Arg.Any<AiTestInput>(), Arg.Any<CancellationToken>())
            .Returns(ConnectionHealth.Healthy("ok", 5));
        _handler = new SaveAiConfigCommandHandler(
            _contextMock,
            _tenantMock,
            _encryptionServiceMock,
            _publishEndpointMock,
            _testerMock,
            _loggerMock);
    }

    private SaveAiConfigCommand Command(
        string? apiKey,
        AiProviderType provider = AiProviderType.OpenAI,
        string model = "gpt-4o",
        bool isActive = true)
        => new(provider, apiKey, null, model, null, isActive, _actorId);

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
    public async Task Handle_CreatesConfig_StampingTenantAndActor_WhenNoneExists()
    {
        _encryptionServiceMock.Encrypt("raw-api-key").Returns("encrypted-api-key");
        SetupConfigs();

        var result = await _handler.Handle(Command("raw-api-key"), CancellationToken.None);

        result.Should().NotBeEmpty();
        _contextMock.WorkspaceAiConfigs.Received(1).Add(Arg.Is<WorkspaceAiConfig>(x =>
            x.TenantId == _tenantId &&
            x.Provider == AiProviderType.OpenAI &&
            x.EncryptedApiKey == "encrypted-api-key" &&
            x.CreatedByUserId == _actorId &&
            x.IsActive));
        await _contextMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _publishEndpointMock.Received(1).Publish(Arg.Any<AiConfigUpdatedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Throws_WhenCreatingWithoutApiKey()
    {
        SetupConfigs();

        Func<Task> act = () => _handler.Handle(Command(apiKey: ""), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
        await _contextMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RetainsStoredKey_WhenUpdatingWithEmptyApiKey()
    {
        var existing = new WorkspaceAiConfig(_tenantId, AiProviderType.OpenAI, "stored-encrypted", null, "gpt-4o", null, null, true);
        SetupConfigs(existing);

        await _handler.Handle(Command(apiKey: "", model: "gpt-4o-mini"), CancellationToken.None);

        existing.EncryptedApiKey.Should().Be("stored-encrypted");
        existing.DefaultModel.Should().Be("gpt-4o-mini");
        _encryptionServiceMock.DidNotReceive().Encrypt(Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_UpdatesAndDeactivatesOthers_WhenSetActive()
    {
        var target = new WorkspaceAiConfig(_tenantId, AiProviderType.Anthropic, "old-key", null, "claude-old", null, null, false);
        var otherActive = new WorkspaceAiConfig(_tenantId, AiProviderType.OpenAI, "other-key", null, "gpt-4o", null, null, true);
        SetupConfigs(target, otherActive);
        _encryptionServiceMock.Encrypt("new-api-key").Returns("new-encrypted");

        var result = await _handler.Handle(
            Command("new-api-key", AiProviderType.Anthropic, "claude-3-opus"),
            CancellationToken.None);

        result.Should().Be(target.Id);
        target.EncryptedApiKey.Should().Be("new-encrypted");
        target.DefaultModel.Should().Be("claude-3-opus");
        target.IsActive.Should().BeTrue();
        otherActive.IsActive.Should().BeFalse();
        await _publishEndpointMock.Received(1).Publish(Arg.Any<AiConfigUpdatedEvent>(), Arg.Any<CancellationToken>());
    }
}
