using NexConvo.Chat.Domain.Enums;
using NexConvo.Contracts.Enums;

namespace NexConvo.Chat.Application.Features.ChatSettings.Dtos;

public sealed record WorkspaceChatSettingsDto(
    Guid? Id,
    AiProviderType PrimaryProvider,
    string PrimaryModel,
    IReadOnlyList<AiProviderType> FallbackProviders,
    string? SystemPromptOverride,
    double HandoffConfidenceThreshold,
    bool SentimentEscalationEnabled,
    SentimentSensitivity SentimentSensitivity,
    IReadOnlyList<string> TriggerPhrases,
    int MaxUnansweredMessages,
    PiiMaskingLevel PiiMaskingLevel,
    int? DataRetentionDays,
    DateTimeOffset? UpdatedAt);
