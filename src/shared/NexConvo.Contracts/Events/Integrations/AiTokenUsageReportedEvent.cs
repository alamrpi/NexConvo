using System;

namespace NexConvo.Contracts.Events.Integrations;

public record AiTokenUsageReportedEvent(
    Guid TenantId,
    Guid UserId,
    string Provider,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    DateTimeOffset Timestamp) : IntegrationEvent;
