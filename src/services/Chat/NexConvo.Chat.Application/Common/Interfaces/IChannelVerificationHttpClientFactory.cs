using NexConvo.Contracts.Enums;

namespace NexConvo.Chat.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the Polly-wrapped HTTP clients used to verify external channel connections.
/// The Infrastructure layer provides the concrete implementation, wiring each client to its
/// resilience policy (Standard 8).
/// </summary>
public interface IChannelVerificationHttpClientFactory
{
    /// <summary>Returns the appropriate pre-configured HttpClient for the given channel.</summary>
    HttpClient CreateClient(LeadSourceChannel channel);
}
