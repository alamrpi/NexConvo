namespace NexConvo.Contracts.Events.Chat;

/// <summary>Reason is a string (not the Chat-owned EscalationReason enum) to avoid cross-service
/// enum-drift — mirrors how AiConfigUpdatedEvent.Provider is a string, not an enum.</summary>
public sealed record ConversationHandoffRequestedIntegrationEvent(
    Guid TenantId, Guid ConversationId, string Reason, DateTimeOffset RaisedAt) : IntegrationEvent;
