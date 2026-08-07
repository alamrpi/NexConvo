using MassTransit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NexConvo.Chat.Application.Features.Health;
using NexConvo.Chat.Application.Features.Health.EventHandlers;
using NexConvo.Contracts.Messages;

namespace NexConvo.Chat.Application.UnitTests;

public class CheckIntegrationHealthCommandConsumerTests
{
    private readonly IChatHealthSweepService _sweepServiceMock = Substitute.For<IChatHealthSweepService>();
    private readonly ILogger<CheckIntegrationHealthCommandConsumer> _loggerMock =
        Substitute.For<ILogger<CheckIntegrationHealthCommandConsumer>>();

    [Fact]
    public async Task Consume_delegates_to_sweep_service()
    {
        var consumer = new CheckIntegrationHealthCommandConsumer(_sweepServiceMock, _loggerMock);
        var contextMock = Substitute.For<ConsumeContext<CheckIntegrationHealthCommand>>();
        contextMock.Message.Returns(new CheckIntegrationHealthCommand());
        contextMock.CancellationToken.Returns(CancellationToken.None);

        await consumer.Consume(contextMock);

        await _sweepServiceMock.Received(1).RunAsync(Arg.Any<CancellationToken>());
    }
}
