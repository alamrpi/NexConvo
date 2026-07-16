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

        // ── Widget configuration (rendered on public sites — validate server-side, audit H4) ──
        // #RGB, #RRGGBB, or #RRGGBBAA hex colors only.
        const string hexColorPattern = "^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$";

        RuleFor(x => x.WidgetPrimaryColor)
            .NotEmpty().WithMessage("Primary color is required.")
            .Matches(hexColorPattern).WithMessage("Primary color must be a hex color (e.g. #0F172A).");

        RuleFor(x => x.WidgetSecondaryColor)
            .NotEmpty().WithMessage("Secondary color is required.")
            .Matches(hexColorPattern).WithMessage("Secondary color must be a hex color (e.g. #3B82F6).");

        // Optional icon URL — must be an absolute https URL (blocks javascript:/data:/http: XSS vectors).
        RuleFor(x => x.WidgetIconUrl)
            .Must(BeAnHttpsUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.WidgetIconUrl))
            .WithMessage("Widget icon URL must be an absolute https URL.");

        RuleFor(x => x.WidgetWelcomeMessage)
            .NotEmpty().WithMessage("Welcome message is required.")
            .MaximumLength(500).WithMessage("Welcome message must not exceed 500 characters.");
    }

    private static bool BeAnHttpsUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}
