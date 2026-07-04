using FluentValidation;

namespace NexConvo.Chat.Application.Features.ChatSettings.Commands;

public sealed class SaveChatSettingsCommandValidator : AbstractValidator<SaveChatSettingsCommand>
{
    public SaveChatSettingsCommandValidator()
    {
        RuleFor(x => x.PrimaryModel)
            .NotEmpty().WithMessage("Primary model is required.")
            .MaximumLength(200);

        RuleFor(x => x.HandoffConfidenceThreshold)
            .InclusiveBetween(0.1, 1.0)
            .WithMessage("Handoff confidence threshold must be between 0.1 and 1.0.");

        RuleFor(x => x.TriggerPhrases)
            .Must(p => p.Count <= 50)
            .WithMessage("Trigger phrases must not exceed 50 items.");

        RuleFor(x => x.MaxUnansweredMessages)
            .InclusiveBetween(1, 20)
            .WithMessage("Max unanswered messages must be between 1 and 20.");

        RuleFor(x => x.DataRetentionDays)
            .GreaterThanOrEqualTo(7)
            .When(x => x.DataRetentionDays.HasValue)
            .WithMessage("Data retention days must be at least 7.");
    }
}
