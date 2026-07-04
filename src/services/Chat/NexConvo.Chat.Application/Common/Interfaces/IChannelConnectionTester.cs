using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Application.Common.Interfaces;

/// <summary>
/// Tests whether a given access token is valid for the specified channel.
/// Implemented in Infrastructure per-channel (Polly-wrapped, Standard 8).
/// Web channel short-circuits in the handler without calling this.
/// </summary>
public interface IChannelConnectionTester
{
    Task<ChannelTestResult> TestAsync(ChatChannel channel, string accessToken, CancellationToken ct);
}

public sealed record ChannelTestResult(bool Success, string? AccountName, string? ErrorMessage);
