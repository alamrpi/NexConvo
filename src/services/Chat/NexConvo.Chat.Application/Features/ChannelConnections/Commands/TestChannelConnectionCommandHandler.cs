using MediatR;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Application.Health;
using NexConvo.Chat.Application.Common;

namespace NexConvo.Chat.Application.Features.ChannelConnections.Commands;

public sealed class TestChannelConnectionCommandHandler(
    IConnectionTester<ChannelTestInput> tester,
    ILogger<TestChannelConnectionCommandHandler> logger)
    : IRequestHandler<TestChannelConnectionCommand, TestChannelConnectionResult>
{
    public async Task<TestChannelConnectionResult> Handle(
        TestChannelConnectionCommand cmd,
        CancellationToken ct)
    {
        try
        {
            var result = await tester.TestAsync(new ChannelTestInput(cmd.Channel, cmd.AccessToken, null), ct);
            return new TestChannelConnectionResult(result.Success, result.Detail, result.ErrorMessage);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Channel connection test failed for {Channel}: {ExceptionType}",
                cmd.Channel, ex.GetType().Name);
            return new TestChannelConnectionResult(false, null, "Connection test failed.");
        }
    }
}
