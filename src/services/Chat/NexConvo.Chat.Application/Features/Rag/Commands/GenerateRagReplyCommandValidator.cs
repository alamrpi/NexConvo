using FluentValidation;

namespace NexConvo.Chat.Application.Features.Rag.Commands;

public sealed class GenerateRagReplyCommandValidator : AbstractValidator<GenerateRagReplyCommand>
{
    public GenerateRagReplyCommandValidator() =>
        RuleFor(x => x.ConversationId).NotEmpty();
}
