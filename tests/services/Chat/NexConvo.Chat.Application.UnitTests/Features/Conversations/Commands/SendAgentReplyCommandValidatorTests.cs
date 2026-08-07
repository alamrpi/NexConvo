using FluentValidation.TestHelper;
using NexConvo.Chat.Application.Features.Conversations.Commands;

namespace NexConvo.Chat.Application.UnitTests.Features.Conversations.Commands;

public class SendAgentReplyCommandValidatorTests
{
    private readonly SendAgentReplyCommandValidator _validator = new();

    [Fact]
    public void ValidCommand_Passes()
    {
        var result = _validator.TestValidate(
            new SendAgentReplyCommand(Guid.NewGuid(), Guid.NewGuid(), "Hello, how can I help?"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyText_Fails()
    {
        var result = _validator.TestValidate(
            new SendAgentReplyCommand(Guid.NewGuid(), Guid.NewGuid(), ""));

        result.ShouldHaveValidationErrorFor(x => x.Text);
    }

    [Fact]
    public void TextTooLong_Fails()
    {
        var result = _validator.TestValidate(
            new SendAgentReplyCommand(Guid.NewGuid(), Guid.NewGuid(), new string('a', 8001)));

        result.ShouldHaveValidationErrorFor(x => x.Text);
    }

    [Fact]
    public void EmptyConversationId_Fails()
    {
        var result = _validator.TestValidate(
            new SendAgentReplyCommand(Guid.Empty, Guid.NewGuid(), "hi"));

        result.ShouldHaveValidationErrorFor(x => x.ConversationId);
    }
}
