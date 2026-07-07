using NexConvo.Chat.Domain.Enums;
using NexConvo.Contracts.Enums;

namespace NexConvo.Chat.Application.Common;

public static class ChatChannelExtensions
{
    /// <summary>
    /// Maps the Chat service's channel enum to the shared cross-service <see cref="LeadSourceChannel"/>
    /// discriminator. Public because Chat.Infrastructure (e.g. <c>ChannelConnectionTester</c>) needs it
    /// to select the correct named HttpClient per channel via <c>IChannelVerificationHttpClientFactory</c>.
    /// </summary>
    public static LeadSourceChannel ToLeadSourceChannel(this ChatChannel channel) => channel switch
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
