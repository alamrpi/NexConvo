using MediatR;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Contracts.Enums;

namespace NexConvo.Chat.Application.Features.ChatSettings.Commands;

/// <summary>
/// Upserts the chatbot configuration for the current tenant (one row per tenant).
/// Tenant resolved from JWT (ITenantContext) — Standard 6.
/// </summary>
public sealed record SaveChatSettingsCommand(
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
    int? DataRetentionDays,
    string? WidgetIconUrl,
    string WidgetPrimaryColor,
    string WidgetSecondaryColor,
    string WidgetWelcomeMessage,
    string NoAnswerMessage,
    Guid ActorUserId) : IRequest<Result<Guid>>;
