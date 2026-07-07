using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NSubstitute;
using NexConvo.BuildingBlocks.Application.Health;
using NexConvo.BuildingBlocks.Application.Security;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.Chat.Application.Common;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Domain.Entities;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Chat.Infrastructure.HealthCheck;
using NexConvo.Contracts.Events.Health;

namespace NexConvo.Chat.Infrastructure.Tests;

public class ChatHealthSweepServiceTests
{
    private readonly IChatDbContext _contextMock = Substitute.For<IChatDbContext>();
    private readonly IAesEncryptionService _aesMock = Substitute.For<IAesEncryptionService>();
    private readonly IConnectionTester<ChannelTestInput> _channelTesterMock = Substitute.For<IConnectionTester<ChannelTestInput>>();
    private readonly IPublishEndpoint _publishEndpointMock = Substitute.For<IPublishEndpoint>();
    private readonly ILogger<ChatHealthSweepService> _loggerMock = Substitute.For<ILogger<ChatHealthSweepService>>();

    private readonly ChatHealthSweepService _sut;

    public ChatHealthSweepServiceTests()
    {
        _aesMock.Decrypt(Arg.Any<string>()).Returns(ci => "dec:" + ci.Arg<string>());
        _channelTesterMock.IntegrationKind.Returns("channel");

        // The production sweep builds its own owner-connection DbContext internally; for unit
        // tests we inject a factory that returns our NSubstitute IChatDbContext instead, so the
        // per-connection transition/persist/publish logic is directly testable without a real
        // Postgres connection.
        _sut = new ChatHealthSweepService(
            () => _contextMock,
            _channelTesterMock,
            _aesMock,
            _publishEndpointMock,
            _loggerMock);
    }

    private static ChannelConnection MakeConnection(Guid tenantId, ConnectionStatus previousStatus, string externalAccountId = "ext-1")
    {
        var connection = new ChannelConnection(
            tenantId, ChatChannel.Telegram, externalAccountId, "My Bot", "ENC_TOKEN");

        if (previousStatus != ConnectionStatus.Untested)
        {
            var health = previousStatus == ConnectionStatus.Healthy
                ? ConnectionHealth.Healthy("ok", 10)
                : ConnectionHealth.Failed("previously bad", 10);
            connection.ApplyHealth(health);
        }

        return connection;
    }

    private void SetupConnections(params ChannelConnection[] connections)
    {
        var set = connections.ToList().AsQueryable().BuildMockDbSet();
        _contextMock.ChannelConnections.Returns(set);
    }

    [Fact]
    public async Task RunAsync_probe_healthy_persists_and_publishes_no_event()
    {
        var tenantId = Guid.NewGuid();
        var connection = MakeConnection(tenantId, ConnectionStatus.Untested);
        SetupConnections(connection);

        _channelTesterMock.TestAsync(Arg.Any<ChannelTestInput>(), Arg.Any<CancellationToken>())
            .Returns(ConnectionHealth.Healthy("reachable", 15));

        await _sut.RunAsync(CancellationToken.None);

        connection.LastTestStatus.Should().Be(ConnectionStatus.Healthy);
        await _contextMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _publishEndpointMock.DidNotReceive().Publish(Arg.Any<IntegrationHealthFailedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_previously_healthy_probe_fails_persists_and_publishes_once()
    {
        var tenantId = Guid.NewGuid();
        var connection = MakeConnection(tenantId, ConnectionStatus.Healthy, "ext-42");
        SetupConnections(connection);

        _channelTesterMock.TestAsync(Arg.Any<ChannelTestInput>(), Arg.Any<CancellationToken>())
            .Returns(ConnectionHealth.Failed("sanitized: access denied", 20));

        await _sut.RunAsync(CancellationToken.None);

        connection.LastTestStatus.Should().Be(ConnectionStatus.Failed);
        await _contextMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        await _publishEndpointMock.Received(1).Publish(
            Arg.Is<IntegrationHealthFailedEvent>(e =>
                e.TenantId == tenantId &&
                e.IntegrationKind == "channel" &&
                e.ConfigId == connection.Id &&
                e.ConfigName == $"{ChatChannel.Telegram}:ext-42" &&
                e.ErrorMessage == "sanitized: access denied" &&
                e.PreviousStatus == nameof(ConnectionStatus.Healthy)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_previously_failed_probe_fails_persists_but_does_not_republish()
    {
        var tenantId = Guid.NewGuid();
        var connection = MakeConnection(tenantId, ConnectionStatus.Failed);
        SetupConnections(connection);

        _channelTesterMock.TestAsync(Arg.Any<ChannelTestInput>(), Arg.Any<CancellationToken>())
            .Returns(ConnectionHealth.Failed("still bad", 20));

        await _sut.RunAsync(CancellationToken.None);

        connection.LastTestStatus.Should().Be(ConnectionStatus.Failed);
        await _contextMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _publishEndpointMock.DidNotReceive().Publish(Arg.Any<IntegrationHealthFailedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_one_connection_throws_sweep_continues_to_next_connection()
    {
        var tenantId1 = Guid.NewGuid();
        var tenantId2 = Guid.NewGuid();
        var throwingConnection = new ChannelConnection(
            tenantId1, ChatChannel.WhatsApp, "throwing-ext", "Bad Bot", "ENC_TOKEN");
        var okConnection = new ChannelConnection(
            tenantId2, ChatChannel.Telegram, "ok-ext", "Good Bot", "ENC_TOKEN");
        SetupConnections(throwingConnection, okConnection);

        _channelTesterMock.TestAsync(
                Arg.Is<ChannelTestInput>(x => x.ExternalAccountId == "throwing-ext"),
                Arg.Any<CancellationToken>())
            .Returns<Task<ConnectionHealth>>(_ => throw new InvalidOperationException("boom"));

        _channelTesterMock.TestAsync(
                Arg.Is<ChannelTestInput>(x => x.ExternalAccountId == "ok-ext"),
                Arg.Any<CancellationToken>())
            .Returns(ConnectionHealth.Healthy("ok", 5));

        await _sut.RunAsync(CancellationToken.None);

        okConnection.LastTestStatus.Should().Be(ConnectionStatus.Healthy);
        throwingConnection.LastTestStatus.Should().Be(ConnectionStatus.Untested);
        await _contextMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_only_tests_active_connections()
    {
        var tenantId = Guid.NewGuid();
        var inactiveConnection = new ChannelConnection(
            tenantId, ChatChannel.Telegram, "inactive-ext", "Bot", "ENC_TOKEN");
        inactiveConnection.SetActive(false);
        SetupConnections(inactiveConnection);

        await _sut.RunAsync(CancellationToken.None);

        await _channelTesterMock.DidNotReceive().TestAsync(Arg.Any<ChannelTestInput>(), Arg.Any<CancellationToken>());
    }
}
