using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Contracts.Enums;

namespace NexConvo.Chat.Infrastructure.ExternalServices;

/// <summary>
/// Provides the Polly-wrapped HttpClient for each channel's verification API.
/// Keyed named clients are registered in DependencyInjection.cs with AddNexConvoResilience().
/// </summary>
internal sealed class ChannelVerificationHttpClientFactory(IHttpClientFactory httpClientFactory)
    : IChannelVerificationHttpClientFactory
{
    private const string MetaClientName = "MetaGraphApi";
    private const string TelegramClientName = "TelegramApi";
    private const string DefaultClientName = "ChannelVerification";

    public HttpClient CreateClient(LeadSourceChannel channel)
        => channel switch
        {
            LeadSourceChannel.WhatsApp => httpClientFactory.CreateClient(MetaClientName),
            LeadSourceChannel.Facebook => httpClientFactory.CreateClient(MetaClientName),
            LeadSourceChannel.Instagram => httpClientFactory.CreateClient(MetaClientName),
            LeadSourceChannel.Telegram => httpClientFactory.CreateClient(TelegramClientName),
            _ => httpClientFactory.CreateClient(DefaultClientName),
        };
}
