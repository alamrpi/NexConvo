using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Infrastructure.Services;

// TODO: Implement per-channel token validation via Polly-wrapped HTTP calls (Standard 8).
internal sealed class ChannelConnectionTester : IChannelConnectionTester
{
    public Task<ChannelTestResult> TestAsync(ChatChannel channel, string accessToken, CancellationToken ct)
        => Task.FromResult(new ChannelTestResult(true, null, null));
}
