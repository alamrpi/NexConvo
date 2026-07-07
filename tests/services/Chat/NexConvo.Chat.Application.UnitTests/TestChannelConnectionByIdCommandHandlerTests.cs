using FluentAssertions;
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

namespace NexConvo.Chat.Application.UnitTests;

public class TestChannelConnectionByIdCommandHandlerTests
{
    private readonly IChatDbContext _db = Substitute.For<IChatDbContext>();
    private readonly ITenantContext _tenant = Substitute.For<ITenantContext>();
    private readonly IAesEncryptionService _aes = Substitute.For<IAesEncryptionService>();

    private readonly Guid _tenantId = Guid.NewGuid();

    public TestChannelConnectionByIdCommandHandlerTests()
    {
        _tenant.TenantId.Returns(_tenantId);
        _aes.Decrypt(Arg.Any<string>()).Returns(ci => "dec:" + ci.Arg<string>());
    }

    private void SetupConnections(params ChannelConnection[] connections)
    {
        var set = connections.ToList().AsQueryable().BuildMockDbSet();
        _db.ChannelConnections.Returns(set);
    }

    [Fact]
    public async Task Handle_decrypts_stored_token_tests_it_and_persists_health()
    {
        var connection = new ChannelConnection(
            _tenantId, ChatChannel.Telegram, "ext-1", "My Bot", "ENC_TOKEN");
        SetupConnections(connection);

        var tester = new CapturingTester(ConnectionHealth.Healthy("ok", 10));
        var handler = new TestChannelConnectionByIdCommandHandler(_db, _tenant, _aes, tester);

        var result = await handler.Handle(
            new TestChannelConnectionByIdCommand(connection.Id), CancellationToken.None);

        tester.Last.Should().Be(new ChannelTestInput(ChatChannel.Telegram, "dec:ENC_TOKEN", "ext-1"));
        result.Should().Be(ConnectionHealth.Healthy("ok", 10));
        connection.LastTestStatus.Should().Be(ConnectionStatus.Healthy);
        connection.LastTestLatencyMs.Should().Be(10);
        await _db.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_persists_failed_health_when_test_fails()
    {
        var connection = new ChannelConnection(
            _tenantId, ChatChannel.WhatsApp, "ext-2", "WA", "ENC_TOKEN");
        SetupConnections(connection);

        var tester = new CapturingTester(ConnectionHealth.Failed("bad token", 5));
        var handler = new TestChannelConnectionByIdCommandHandler(_db, _tenant, _aes, tester);

        var result = await handler.Handle(
            new TestChannelConnectionByIdCommand(connection.Id), CancellationToken.None);

        result.Success.Should().BeFalse();
        connection.LastTestStatus.Should().Be(ConnectionStatus.Failed);
        connection.LastTestError.Should().Be("bad token");
        await _db.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_returns_not_found_and_does_not_call_tester_when_connection_missing()
    {
        SetupConnections();

        var tester = new CapturingTester(ConnectionHealth.Healthy("ok", 1));
        var handler = new TestChannelConnectionByIdCommandHandler(_db, _tenant, _aes, tester);

        var result = await handler.Handle(
            new TestChannelConnectionByIdCommand(Guid.NewGuid()), CancellationToken.None);

        result.Should().Be(ConnectionHealth.Failed("Connection not found.", null));
        tester.Last.Should().BeNull();
        await _db.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_does_not_return_connection_belonging_to_another_tenant()
    {
        var otherTenantConnection = new ChannelConnection(
            Guid.NewGuid(), ChatChannel.Telegram, "ext-3", "Other", "ENC_TOKEN");
        SetupConnections(otherTenantConnection);

        var tester = new CapturingTester(ConnectionHealth.Healthy("ok", 1));
        var handler = new TestChannelConnectionByIdCommandHandler(_db, _tenant, _aes, tester);

        var result = await handler.Handle(
            new TestChannelConnectionByIdCommand(otherTenantConnection.Id), CancellationToken.None);

        result.Should().Be(ConnectionHealth.Failed("Connection not found.", null));
        tester.Last.Should().BeNull();
    }

    private sealed class CapturingTester(ConnectionHealth toReturn) : IConnectionTester<ChannelTestInput>
    {
        public ChannelTestInput? Last { get; private set; }

        public string IntegrationKind => "channel";

        public Task<ConnectionHealth> TestAsync(ChannelTestInput input, CancellationToken ct)
        {
            Last = input;
            return Task.FromResult(toReturn);
        }
    }
}
