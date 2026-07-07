using MassTransit;
using Microsoft.Extensions.Logging;
using NexConvo.Automation.Infrastructure.Jobs;
using NexConvo.Contracts.Messages;
using NSubstitute;

namespace NexConvo.Automation.Tests;

public class IntegrationHealthSweepSchedulerTests
{
    [Fact]
    public async Task Trigger_PublishesExactlyOneCheckIntegrationHealthCommand()
    {
        var publishEndpoint = Substitute.For<IPublishEndpoint>();
        var logger = Substitute.For<ILogger<IntegrationHealthSweepScheduler>>();
        var sut = new IntegrationHealthSweepScheduler(publishEndpoint, logger);

        await sut.Trigger();

        await publishEndpoint
            .Received(1)
            .Publish(Arg.Any<CheckIntegrationHealthCommand>(), Arg.Any<CancellationToken>());
    }
}
