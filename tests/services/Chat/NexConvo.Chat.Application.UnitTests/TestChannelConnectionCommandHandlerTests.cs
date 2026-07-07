using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NexConvo.BuildingBlocks.Application.Health;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.Chat.Application.Common;
using NexConvo.Chat.Application.Features.ChannelConnections.Commands;
using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Application.UnitTests;

public class TestChannelConnectionCommandHandlerTests
{
    private readonly ILogger<TestChannelConnectionCommandHandler> _logger =
        Substitute.For<ILogger<TestChannelConnectionCommandHandler>>();

    [Fact]
    public async Task Handle_passes_explicit_token_to_tester_and_returns_its_health()
    {
        var tester = new CapturingTester(ConnectionHealth.Healthy("ok", 42));
        var handler = new TestChannelConnectionCommandHandler(tester, _logger);

        var result = await handler.Handle(
            new TestChannelConnectionCommand(ChatChannel.Telegram, "TOKEN123", "ext-1"),
            CancellationToken.None);

        tester.Last.Should().Be(new ChannelTestInput(ChatChannel.Telegram, "TOKEN123", "ext-1"));
        result.Should().Be(ConnectionHealth.Healthy("ok", 42));
    }

    [Fact]
    public async Task Handle_returns_failed_health_when_tester_throws()
    {
        var tester = new ThrowingTester();
        var handler = new TestChannelConnectionCommandHandler(tester, _logger);

        var result = await handler.Handle(
            new TestChannelConnectionCommand(ChatChannel.Telegram, "TOKEN123", null),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Status.Should().Be(ConnectionStatus.Failed);
        result.ErrorMessage.Should().Be("Connection test failed.");
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

    private sealed class ThrowingTester : IConnectionTester<ChannelTestInput>
    {
        public string IntegrationKind => "channel";

        public Task<ConnectionHealth> TestAsync(ChannelTestInput input, CancellationToken ct)
            => throw new InvalidOperationException("boom");
    }
}
