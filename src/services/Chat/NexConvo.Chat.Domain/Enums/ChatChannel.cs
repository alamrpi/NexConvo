namespace NexConvo.Chat.Domain.Enums;

/// <summary>
/// The omnichannel platform this ChannelConnection is bound to.
/// Web is the built-in live-chat widget — no external token is required.
/// </summary>
public enum ChatChannel
{
    Web = 0,
    WhatsApp = 1,
    Facebook = 2,
    Instagram = 3,
    Telegram = 4,
    TikTok = 5,
    LinkedIn = 6,
}
