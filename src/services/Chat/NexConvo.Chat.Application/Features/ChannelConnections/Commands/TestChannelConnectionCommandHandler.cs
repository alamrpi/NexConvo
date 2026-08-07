using MediatR;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Application.Health;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.Chat.Application.Common;

namespace NexConvo.Chat.Application.Features.ChannelConnections.Commands;

public sealed class TestChannelConnectionCommandHandler(
    IConnectionTester<ChannelTestInput> tester,
    ILogger<TestChannelConnectionCommandHandler> logger)
    : IRequestHandler<TestChannelConnectionCommand, ConnectionHealth>
{
    public async Task<ConnectionHealth> Handle(
        TestChannelConnectionCommand cmd,
        CancellationToken ct)
    {
        try
        {
            return await tester.TestAsync(
                new ChannelTestInput(cmd.Channel, cmd.AccessToken, cmd.ExternalAccountId), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Channel connection test failed for {Channel}: {ExceptionType}",
                cmd.Channel, ex.GetType().Name);
            return ConnectionHealth.Failed("Connection test failed.", null);
        }
    }
}
