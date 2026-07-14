using NexConvo.BuildingBlocks.Domain;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Contracts.Enums;

namespace NexConvo.Chat.Domain.Entities;

/// <summary>
/// Built-in "speak to a human" trigger phrases seeded for a tenant's very first
/// <see cref="WorkspaceChatSettings"/> row when they haven't configured any of their own
/// (D4-8: previously TriggerPhrases defaulted to empty, so a tenant who never customized this
/// field got zero trigger-phrase handoffs out of the box — including in Bengali, a first-class
/// requirement per CLAUDE.md). Only used at first-create time; never overrides a tenant's own
/// saved value, including an intentional empty list on a later save.
/// </summary>
public static class DefaultTriggerPhrases
{
    public static readonly IReadOnlyList<string> Values =
    [
        "speak to a manager",
        "talk to a human",
        "human agent",
        "ম্যানেজারের সাথে কথা বলতে চাই",
        "একজন মানুষের সাথে কথা বলতে চাই",
    ];
}

/// <summary>
/// Per-tenant chatbot configuration: AI provider selection, escalation behaviour, and privacy.
/// One row per tenant — upserted on save.
/// </summary>
public class WorkspaceChatSettings : BaseAggregateRoot
{
    public AiProviderType PrimaryProvider { get; private set; }
    public string PrimaryModel { get; private set; } = null!;

    /// <summary>JSONB column — stored as a JSON array of provider name strings, e.g. ["OpenAI"].</summary>
    public string FallbackProviders { get; private set; } = "[]";

    public string? SystemPromptOverride { get; private set; }
    public double HandoffConfidenceThreshold { get; private set; }
    public bool SentimentEscalationEnabled { get; private set; }
    public SentimentSensitivity SentimentSensitivity { get; private set; }

    /// <summary>JSONB column — stored as a JSON array of phrase strings.</summary>
    public string TriggerPhrases { get; private set; } = "[]";

    public int MaxUnansweredMessages { get; private set; }
    public PiiMaskingLevel PiiMaskingLevel { get; private set; }
    public int? DataRetentionDays { get; private set; }

    private WorkspaceChatSettings() { }

    public WorkspaceChatSettings(
        Guid tenantId,
        AiProviderType primaryProvider,
        string primaryModel,
        string fallbackProviders,
        string? systemPromptOverride,
        double handoffConfidenceThreshold,
        bool sentimentEscalationEnabled,
        SentimentSensitivity sentimentSensitivity,
        string triggerPhrases,
        int maxUnansweredMessages,
        PiiMaskingLevel piiMaskingLevel,
        int? dataRetentionDays)
    {
        TenantId = tenantId;
        PrimaryProvider = primaryProvider;
        PrimaryModel = primaryModel;
        FallbackProviders = fallbackProviders;
        SystemPromptOverride = systemPromptOverride;
        HandoffConfidenceThreshold = handoffConfidenceThreshold;
        SentimentEscalationEnabled = sentimentEscalationEnabled;
        SentimentSensitivity = sentimentSensitivity;
        TriggerPhrases = triggerPhrases;
        MaxUnansweredMessages = maxUnansweredMessages;
        PiiMaskingLevel = piiMaskingLevel;
        DataRetentionDays = dataRetentionDays;
    }

    public void Update(
        AiProviderType primaryProvider,
        string primaryModel,
        string fallbackProviders,
        string? systemPromptOverride,
        double handoffConfidenceThreshold,
        bool sentimentEscalationEnabled,
        SentimentSensitivity sentimentSensitivity,
        string triggerPhrases,
        int maxUnansweredMessages,
        PiiMaskingLevel piiMaskingLevel,
        int? dataRetentionDays)
    {
        PrimaryProvider = primaryProvider;
        PrimaryModel = primaryModel;
        FallbackProviders = fallbackProviders;
        SystemPromptOverride = systemPromptOverride;
        HandoffConfidenceThreshold = handoffConfidenceThreshold;
        SentimentEscalationEnabled = sentimentEscalationEnabled;
        SentimentSensitivity = sentimentSensitivity;
        TriggerPhrases = triggerPhrases;
        MaxUnansweredMessages = maxUnansweredMessages;
        PiiMaskingLevel = piiMaskingLevel;
        DataRetentionDays = dataRetentionDays;
    }
}
