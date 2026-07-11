namespace NexConvo.Contracts.Events.Integrations;

public record S3ConfigUpdatedEvent(
    Guid TenantId,
    string BucketName,
    string Region,
    string? CustomEndpoint,
    string? PathPrefix,
    bool IsActive,
    string EncryptedAccessKeyId,
    string EncryptedSecretAccessKey,
    string LastTestStatus,        // mirrors NexConvo.BuildingBlocks.Domain.Health.ConnectionStatus
                                   // (e.g. "Healthy") — kept as string because Contracts has zero
                                   // project references and must stay dependency-free.
    DateTimeOffset? LastTestedAt) : IntegrationEvent;
