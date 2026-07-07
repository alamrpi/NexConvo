using FluentAssertions;
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

public class TestS3ConnectionCommandHandlerTests
{
    private readonly IIntegrationsDbContext _contextMock = Substitute.For<IIntegrationsDbContext>();
    private readonly ITenantContext _tenantMock = Substitute.For<ITenantContext>();
    private readonly IAesEncryptionService _encryptionServiceMock = Substitute.For<IAesEncryptionService>();
    private readonly CapturingTester _tester = new();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly TestS3ConnectionCommandHandler _handler;

    public TestS3ConnectionCommandHandlerTests()
    {
        _tenantMock.TenantId.Returns(_tenantId);
        _encryptionServiceMock.Decrypt(Arg.Any<string>()).Returns(ci => "dec:" + ci.Arg<string>());
        _handler = new TestS3ConnectionCommandHandler(_contextMock, _tenantMock, _encryptionServiceMock, _tester);
    }

    private void SetupConfigs(params WorkspaceS3Config[] configs)
    {
        var set = configs.ToList().AsQueryable().BuildMockDbSet();
        _contextMock.WorkspaceS3Configs.Returns(set);
    }

    [Fact]
    public async Task Uses_explicit_keys_when_provided()
    {
        SetupConfigs();

        var result = await _handler.Handle(
            new TestS3ConnectionCommand("b", "us-east-1", "AK", "SK", null), CancellationToken.None);

        _tester.Last!.AccessKeyId.Should().Be("AK");
        _tester.Last!.SecretAccessKey.Should().Be("SK");
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Falls_back_to_stored_decrypted_keys_when_blank()
    {
        var stored = new WorkspaceS3Config(
            _tenantId, "stored-bucket", "us-east-1", "ENC_AK", "ENC_SK", null, null, true);
        SetupConfigs(stored);

        await _handler.Handle(new TestS3ConnectionCommand("b", "us-east-1", null, null, null), CancellationToken.None);

        _tester.Last!.AccessKeyId.Should().Be("dec:ENC_AK");
        _tester.Last!.SecretAccessKey.Should().Be("dec:ENC_SK");
    }

    [Fact]
    public async Task Returns_failure_when_no_stored_config_and_blank_keys()
    {
        SetupConfigs();

        var result = await _handler.Handle(
            new TestS3ConnectionCommand("b", "us-east-1", "", "", null), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("No S3 credentials configured.");
    }

    private sealed class CapturingTester : IConnectionTester<S3TestInput>
    {
        public S3TestInput? Last { get; private set; }

        public string IntegrationKind => "s3";

        public Task<ConnectionHealth> TestAsync(S3TestInput input, CancellationToken ct)
        {
            Last = input;
            return Task.FromResult(ConnectionHealth.Healthy("ok", 5));
        }
    }
}
