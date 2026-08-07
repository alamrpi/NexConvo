using FluentValidation;

namespace NexConvo.Chat.Application.Features.Conversations.Commands;

public sealed class SendAgentReplyCommandValidator : AbstractValidator<SendAgentReplyCommand>
{
    public SendAgentReplyCommandValidator()
    {
        RuleFor(x => x.ConversationId).NotEmpty();
        RuleFor(x => x.AgentUserId).NotEmpty();

        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Reply text is required.")
            .MaximumLength(8000);
    }
}
