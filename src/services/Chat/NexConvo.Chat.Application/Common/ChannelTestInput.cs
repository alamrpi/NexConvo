using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Application.Common;

/// <summary>
/// Input to <see cref="NexConvo.BuildingBlocks.Application.Health.IConnectionTester{TInput}"/> for the
/// "channel" integration kind. Carries the access token needed to probe the external provider's API
/// (Meta Graph, Telegram Bot API, etc.) plus the optional externally-known account/page id.
/// The access token is never logged (Standard 13).
/// </summary>
public sealed record ChannelTestInput(ChatChannel Channel, string AccessToken, string? ExternalAccountId);
