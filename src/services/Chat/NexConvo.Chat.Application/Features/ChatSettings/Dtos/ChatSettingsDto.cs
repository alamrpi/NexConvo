namespace NexConvo.Chat.Application.Features.ChatSettings.Dtos;

public sealed record ChatSettingsDto(
    Guid Id,
    string BotName,
    string? WelcomeMessage,
    double HandoffConfidenceThreshold,
    List<string>? EscalationTriggerPhrases,
    bool IsEnabled);
