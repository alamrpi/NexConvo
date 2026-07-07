using FluentAssertions;
using MassTransit;
using MockQueryable.NSubstitute;
using NSubstitute;
using NexConvo.BuildingBlocks.Application.Health;
using NexConvo.BuildingBlocks.Application.Security;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.Chat.Application.Common;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Application.Features.ChannelConnections.Commands;
using NexConvo.Chat.Domain.Entities;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Contracts.Events.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NexConvo.Chat.Application.UnitTests;

public class SaveChannelConnectionGateTests
{
    private readonly IChatDbContext _dbMock = Substitute.For<IChatDbContext>();
    private readonly ITenantContext _tenantMock = Substitute.For<ITenantContext>();
    private readonly IAesEncryptionService _encryptionMock = Substitute.For<IAesEncryptionService>();
    private readonly IPublishEndpoint _publisherMock = Substitute.For<IPublishEndpoint>();
    private readonly IConnectionTester<ChannelTestInput> _testerMock = Substitute.For<IConnectionTester<ChannelTestInput>>();
    private readonly Microsoft.Extensions.Logging.ILogger<SaveChannelConnectionCommandHandler> _loggerMock
        = Substitute.For<Microsoft.Extensions.Logging.ILogger<SaveChannelConnectionCommandHandler>>();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly SaveChannelConnectionCommandHandler _handler;

    public SaveChannelConnectionGateTests()
    {
        _tenantMock.TenantId.Returns(_tenantId);
        _encryptionMock.Encrypt(Arg.Any<string>()).Returns(ci => "enc:" + ci.Arg<string>());
        _handler = new SaveChannelConnectionCommandHandler(
            _dbMock,
            _tenantMock,
            _encryptionMock,
            _publisherMock,
            _testerMock,
            _loggerMock);
    }

    private SaveChannelConnectionCommand Command(bool withNewToken = true) => new(
        ChatChannel.WhatsApp,
        "acct-123",
        "My WhatsApp",
        withNewToken ? "token-abc" : string.Empty,
        null,
        _actorId);

    private void SetupConnections(params ChannelConnection[] connections)
    {
        var connSet = connections.ToList().AsQueryable().BuildMockDbSet();
        var auditSet = new List<ChatAuditLog>().AsQueryable().BuildMockDbSet();
        _dbMock.ChannelConnections.Returns(connSet);
        _dbMock.ChatAuditLogs.Returns(auditSet);
    }

    [Fact]
    public async Task Save_rejects_with_ConnectionTestFailedException_when_retest_fails()
    {
        SetupConnections();
        _testerMock.TestAsync(Arg.Any<ChannelTestInput>(), Arg.Any<CancellationToken>())
            .Returns(ConnectionHealth.Failed("invalid token", null));

        Func<Task> act = () => _handler.Handle(Command(withNewToken: true), CancellationToken.None);

        await act.Should().ThrowAsync<ConnectionTestFailedException>();
        _dbMock.ChannelConnections.DidNotReceive().Add(Arg.Any<ChannelConnection>());
        _dbMock.ChatAuditLogs.DidNotReceive().Add(Arg.Any<ChatAuditLog>());
        await _dbMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _publisherMock.DidNotReceive().Publish(Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_persists_and_returns_dto_with_connected_status_on_success()
    {
        SetupConnections();
        _testerMock.TestAsync(Arg.Any<ChannelTestInput>(), Arg.Any<CancellationToken>())
            .Returns(ConnectionHealth.Healthy("ok", 12));

        var result = await _handler.Handle(Command(withNewToken: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().NotBe(Guid.Empty);
        result.Value.Status.Should().Be("connected");
        _dbMock.ChannelConnections.Received(1).Add(Arg.Is<ChannelConnection>(
            x => x.LastTestStatus == ConnectionStatus.Healthy));
        await _dbMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _publisherMock.Received(1).Publish(Arg.Any<ChannelConnectionUpdatedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_skips_retest_when_token_unchanged_for_existing_connection()
    {
        var existing = new ChannelConnection(
            _tenantId, ChatChannel.WhatsApp, "acct-123", "My WhatsApp", "ENC_TOKEN");
        SetupConnections(existing);

        var result = await _handler.Handle(Command(withNewToken: false), CancellationToken.None);

        await _testerMock.DidNotReceive().TestAsync(Arg.Any<ChannelTestInput>(), Arg.Any<CancellationToken>());
        existing.LastTestStatus.Should().Be(ConnectionStatus.Untested);
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("disconnected");
        await _dbMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
