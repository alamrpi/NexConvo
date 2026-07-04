namespace NexConvo.Contracts.Events.Integrations;

public record S3ConfigUpdatedEvent(
    Guid TenantId,
    string BucketName,
    string Region,
    string? CustomEndpoint,
    string? PathPrefix,
    bool IsActive) : IntegrationEvent;
