using NexConvo.Contracts.Enums;

namespace NexConvo.Chat.Domain.ValueObjects;

/// <summary>Identifies which external channel thread a Conversation maps to. Uses the shared Contracts enum, matching MessageReceivedIntegrationEvent's Channel field.</summary>
public sealed record ChannelIdentity(LeadSourceChannel Channel, string ExternalConversationId);
