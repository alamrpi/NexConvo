using FluentValidation;
using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Application.Features.ChannelConnections.Commands;

public sealed class SaveChannelConnectionCommandValidator : AbstractValidator<SaveChannelConnectionCommand>
{
    public SaveChannelConnectionCommandValidator()
    {
        RuleFor(x => x.Channel)
            .IsInEnum()
            .WithMessage("A valid channel must be specified.");

        // Web widget: ExternalAccountId is optional; all other channels require it.
        When(x => x.Channel != ChatChannel.Web, () =>
        {
            RuleFor(x => x.ExternalAccountId)
                .NotEmpty().WithMessage("External account ID is required for non-Web channels.")
                .MaximumLength(200);
        });

        RuleFor(x => x.AccessToken)
            .NotEmpty().WithMessage("Access token is required.")
            .MaximumLength(2000);

        RuleFor(x => x.AccountName)
            .MaximumLength(200)
            .When(x => !string.IsNullOrEmpty(x.AccountName));
    }
}
