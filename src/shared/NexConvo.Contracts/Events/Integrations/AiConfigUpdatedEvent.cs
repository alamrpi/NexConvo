using System;

namespace NexConvo.Contracts.Events.Integrations;

public record AiConfigUpdatedEvent(
    Guid TenantId,
    string Provider,
    string EncryptedApiKey,
    string? BaseUrl,
    string DefaultModel,
    string? SystemPrompt,
    string? Parameters,
    bool IsActive) : IntegrationEvent;
