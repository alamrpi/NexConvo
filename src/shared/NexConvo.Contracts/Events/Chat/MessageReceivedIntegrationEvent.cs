using NexConvo.Contracts.Enums;

namespace NexConvo.Contracts.Events.Chat;

/// <summary>
/// Published by the Omnichannel Chat service when an inbound message arrives on any
/// channel. Consumed by AI Command / the chatbot engine to generate a reply.
/// </summary>
public sealed record MessageReceivedIntegrationEvent : IntegrationEvent
{
    public required Guid ConversationId { get; init; }
    public required Guid TenantId { get; init; }
    public required LeadSourceChannel Channel { get; init; }

    /// <summary>External (platform) sender id — not internal PII.</summary>
    public required string ExternalSenderId { get; init; }

    public required string MessageRef { get; init; }
}
