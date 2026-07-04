using MediatR;
using Microsoft.Extensions.Logging;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Application.Features.ChannelConnections.Commands;

public sealed class TestChannelConnectionCommandHandler(
    IChannelConnectionTester tester,
    ILogger<TestChannelConnectionCommandHandler> logger)
    : IRequestHandler<TestChannelConnectionCommand, TestChannelConnectionResult>
{
    public async Task<TestChannelConnectionResult> Handle(
        TestChannelConnectionCommand cmd,
        CancellationToken ct)
    {
        // Web widget needs no external token — short-circuit without HTTP call.
        if (cmd.Channel == ChatChannel.Web)
            return new TestChannelConnectionResult(true, "Web Widget", null);

        try
        {
            var result = await tester.TestAsync(cmd.Channel, cmd.AccessToken, ct);
            return new TestChannelConnectionResult(result.Success, result.AccountName, result.ErrorMessage);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Channel connection test failed for {Channel}: {Message}",
                cmd.Channel, ex.Message);
            return new TestChannelConnectionResult(false, null, "Connection test failed.");
        }
    }
}
