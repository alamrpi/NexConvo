using MassTransit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NexConvo.Contracts.Messages;
using NexConvo.Integrations.Application.Features.Health;
using NexConvo.Integrations.Application.Features.Health.EventHandlers;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NexConvo.Integrations.Application.UnitTests;

public class CheckIntegrationHealthCommandConsumerTests
{
    private readonly IIntegrationsHealthSweepService _sweepServiceMock = Substitute.For<IIntegrationsHealthSweepService>();
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
