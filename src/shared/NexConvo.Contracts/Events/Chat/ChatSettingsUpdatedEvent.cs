using NexConvo.Contracts.Enums;

namespace NexConvo.Contracts.Events.Chat;

/// <summary>
/// Published after workspace chat AI settings are saved.
/// Contains no secrets or PII — only config metadata safe for downstream consumers.
/// </summary>
public sealed record ChatSettingsUpdatedEvent(
    Guid TenantId,
    AiProviderType PrimaryProvider,
    string PrimaryModel,
    bool SentimentEscalationEnabled,
    double HandoffConfidenceThreshold) : IntegrationEvent;
