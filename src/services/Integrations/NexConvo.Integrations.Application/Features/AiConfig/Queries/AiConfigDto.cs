using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.Contracts.Enums;
using System;

namespace NexConvo.Integrations.Application.Features.AiConfig.Queries;

public record AiConfigDto(
    Guid Id,
    AiProviderType Provider,
    bool HasApiKey,
    string? BaseUrl,
    string DefaultModel,
    string? Parameters,
    bool IsActive,
    ConnectionStatus LastTestStatus,
    DateTimeOffset? LastTestedAt,
    string? LastTestError,
    int? LastTestLatencyMs
);
