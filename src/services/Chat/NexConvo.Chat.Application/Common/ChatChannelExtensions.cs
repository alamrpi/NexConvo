using NexConvo.Chat.Domain.Enums;
using NexConvo.Contracts.Enums;

namespace NexConvo.Chat.Application.Common;

internal static class ChatChannelExtensions
{
    internal static LeadSourceChannel ToLeadSourceChannel(this ChatChannel channel) => channel switch
    {
        ChatChannel.WhatsApp => LeadSourceChannel.WhatsApp,
        ChatChannel.Facebook => LeadSourceChannel.Facebook,
        ChatChannel.Instagram => LeadSourceChannel.Instagram,
        ChatChannel.Telegram => LeadSourceChannel.Telegram,
        ChatChannel.TikTok => LeadSourceChannel.TikTok,
        ChatChannel.LinkedIn => LeadSourceChannel.LinkedIn,
        ChatChannel.Web => LeadSourceChannel.Web,
        _ => LeadSourceChannel.Unknown,
    };
}
