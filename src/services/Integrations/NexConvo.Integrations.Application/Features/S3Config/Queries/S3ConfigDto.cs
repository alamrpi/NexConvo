using NexConvo.BuildingBlocks.Domain.Health;

namespace NexConvo.Integrations.Application.Features.S3Config.Queries;

public record S3ConfigDto(
    Guid Id,
    string BucketName,
    string Region,
    bool HasAccessKey,
    string? CustomEndpoint,
    string? PathPrefix,
    bool IsActive,
    ConnectionStatus LastTestStatus,
    DateTimeOffset? LastTestedAt,
    string? LastTestError,
    int? LastTestLatencyMs
);
