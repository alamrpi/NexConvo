using NexConvo.Chat.Domain.Enums;
using NexConvo.Contracts.Enums;

namespace NexConvo.Chat.Api.Models;

/// <summary>
/// Request body for upserting workspace chat settings.
/// Tenant and actor user are resolved server-side from the JWT — never from client input.
/// </summary>
public sealed record SaveChatSettingsRequest(
    AiProviderType PrimaryProvider,
    string PrimaryModel,
    List<AiProviderType> FallbackProviders,
    string? SystemPromptOverride,
    double HandoffConfidenceThreshold,
    bool SentimentEscalationEnabled,
    SentimentSensitivity SentimentSensitivity,
    List<string> TriggerPhrases,
    int MaxUnansweredMessages,
    PiiMaskingLevel PiiMaskingLevel,
    int? DataRetentionDays);
