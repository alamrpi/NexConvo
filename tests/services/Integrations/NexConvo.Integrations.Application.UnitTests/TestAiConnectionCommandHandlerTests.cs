using FluentAssertions;
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

public class TestAiConnectionCommandHandlerTests
{
    private readonly IIntegrationsDbContext _contextMock = Substitute.For<IIntegrationsDbContext>();
    private readonly ITenantContext _tenantMock = Substitute.For<ITenantContext>();
    private readonly IAesEncryptionService _encryptionServiceMock = Substitute.For<IAesEncryptionService>();
    private readonly CapturingTester _tester = new();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly TestAiConnectionCommandHandler _handler;

    public TestAiConnectionCommandHandlerTests()
    {
        _tenantMock.TenantId.Returns(_tenantId);
        _encryptionServiceMock.Decrypt(Arg.Any<string>()).Returns(ci => "dec:" + ci.Arg<string>());
        _handler = new TestAiConnectionCommandHandler(_contextMock, _tenantMock, _encryptionServiceMock, _tester);
    }

    private void SetupConfigs(params WorkspaceAiConfig[] configs)
    {
        var set = configs.ToList().AsQueryable().BuildMockDbSet();
        _contextMock.WorkspaceAiConfigs.Returns(set);
    }

    [Fact]
    public async Task Uses_explicit_key_when_provided()
    {
        SetupConfigs();

        var result = await _handler.Handle(
            new TestAiConnectionCommand(AiProviderType.OpenAI, "raw-key", null, "gpt-4o"), CancellationToken.None);

        _tester.Last!.ApiKey.Should().Be("raw-key");
        _tester.Last!.Provider.Should().Be(AiProviderType.OpenAI);
        _tester.Last!.Model.Should().Be("gpt-4o");
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Falls_back_to_stored_decrypted_key_when_blank()
    {
        var stored = new WorkspaceAiConfig(
            _tenantId, AiProviderType.OpenAI, "ENC_KEY", null, "gpt-4o", null, null, true);
        SetupConfigs(stored);

        await _handler.Handle(
            new TestAiConnectionCommand(AiProviderType.OpenAI, "", null, "gpt-4o"), CancellationToken.None);

        _tester.Last!.ApiKey.Should().Be("dec:ENC_KEY");
    }

    [Fact]
    public async Task Returns_failure_when_no_stored_config_and_blank_key()
    {
        SetupConfigs();

        var result = await _handler.Handle(
            new TestAiConnectionCommand(AiProviderType.OpenAI, null, null, "gpt-4o"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("No API key configured for this provider.");
    }

    private sealed class CapturingTester : IConnectionTester<AiTestInput>
    {
        public AiTestInput? Last { get; private set; }

        public string IntegrationKind => "ai";

        public Task<ConnectionHealth> TestAsync(AiTestInput input, CancellationToken ct)
        {
            Last = input;
            return Task.FromResult(ConnectionHealth.Healthy("ok", 5));
        }
    }
}
