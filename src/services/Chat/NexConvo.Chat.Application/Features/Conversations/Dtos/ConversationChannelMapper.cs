using NexConvo.Contracts.Enums;

namespace NexConvo.Chat.Application.Features.Conversations.Dtos;

/// <summary>
/// Maps the shared cross-service <see cref="LeadSourceChannel"/> discriminator (used by
/// <see cref="NexConvo.Chat.Domain.ValueObjects.ChannelIdentity"/>) to the lowercase channel
/// string the frontend's mock-data-derived <c>Channel</c> union type expects
/// (frontend/src/features/chat/mock-data.ts). Single place this mapping happens so the inbox
/// DTOs and any future channel-filter query stay in lockstep with the frontend union.
/// </summary>
public static class ConversationChannelMapper
{
    public static string ToFrontendChannel(this LeadSourceChannel channel) => channel switch
    {
        LeadSourceChannel.WhatsApp => "whatsapp",
        LeadSourceChannel.Facebook => "facebook",
        LeadSourceChannel.Instagram => "instagram",
        LeadSourceChannel.Telegram => "telegram",
        LeadSourceChannel.Web => "web",
        // TikTok/LinkedIn/Sms/Email/Voice/Manual/Unknown have no dedicated inbox channel pill yet
        // (frontend Channel union is limited to the 5 above) — fall back to the closest neutral
        // value rather than throwing, so an unexpected channel still renders instead of 500ing.
        _ => "web",
    };

    /// <summary>
    /// Parses the frontend's lowercase channel filter query param back to <see cref="LeadSourceChannel"/>.
    /// Returns null for an unrecognized/absent value (meaning "no channel filter").
    /// </summary>
    public static LeadSourceChannel? FromFrontendChannel(string? channel) => channel?.ToLowerInvariant() switch
    {
        "whatsapp" => LeadSourceChannel.WhatsApp,
        "facebook" => LeadSourceChannel.Facebook,
        "instagram" => LeadSourceChannel.Instagram,
        "telegram" => LeadSourceChannel.Telegram,
        "web" => LeadSourceChannel.Web,
        _ => null,
    };
}
