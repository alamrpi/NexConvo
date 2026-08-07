using FluentAssertions;
using NexConvo.BuildingBlocks.Rag;
using Xunit;

namespace NexConvo.BuildingBlocks.Rag.Tests;

public class GroundedPromptAssemblerTests
{
    private readonly GroundedPromptAssembler _sut = new();

    [Fact]
    public void BuildSystemPrompt_ChatProfile_InstructsCitationsAndAbstentionMarker()
    {
        var prompt = _sut.BuildSystemPrompt(ChannelProfile.Chat, tenantSystemPromptOverride: null);

        prompt.Should().Contain("[[NO_ANSWER]]");
        prompt.Should().Contain("only from the numbered context");
        prompt.Should().Contain("[n]");
    }

    [Fact]
    public void BuildSystemPrompt_InstructsMarkerMustNeverBeTranslatedOrLocalized()
    {
        // D4-3: the prompt also tells the model to reply in the user's own language, which would
        // otherwise conflict with emitting an English-only abstention marker for non-English
        // conversations. The instruction must explicitly carve out the marker from that rule.
        var prompt = _sut.BuildSystemPrompt(ChannelProfile.Chat, tenantSystemPromptOverride: null);

        prompt.Should().Contain("Never translate or localize");
        prompt.Should().Contain("[[NO_ANSWER]]");
        prompt.Should().Contain("verbatim");
    }

    [Fact]
    public void BuildSystemPrompt_VoiceProfile_OmitsCitationInstructionAndCapsLength()
    {
        var prompt = _sut.BuildSystemPrompt(ChannelProfile.Voice, tenantSystemPromptOverride: null);

        prompt.Should().Contain("[[NO_ANSWER]]");
        prompt.Should().NotContain("[n]");
        prompt.Should().Contain("1-2 sentence");
    }

    [Fact]
    public void BuildSystemPrompt_WithTenantOverride_AppendsOverrideAfterGroundingRules()
    {
        var prompt = _sut.BuildSystemPrompt(ChannelProfile.Chat, tenantSystemPromptOverride: "Always sign off with 'Team NexConvo'.");

        prompt.Should().Contain("[[NO_ANSWER]]");
        prompt.Should().Contain("Always sign off with 'Team NexConvo'.");
    }

    [Fact]
    public void BuildUserPrompt_ChatProfile_NumbersContextForCitation()
    {
        var context = new[]
        {
            new ContextContribution(1, "chunk-1", "doc-1", "Refunds are processed within 5 business days.", 0.91),
            new ContextContribution(2, "chunk-2", "doc-2", "Contact support for refund status.", 0.77),
        };

        var prompt = _sut.BuildUserPrompt(context, history: [], question: "How long do refunds take?", ChannelProfile.Chat);

        prompt.Should().Contain("[1] Refunds are processed within 5 business days.");
        prompt.Should().Contain("[2] Contact support for refund status.");
        prompt.Should().Contain("How long do refunds take?");
    }

    [Fact]
    public void BuildUserPrompt_IncludesHistoryInOrder()
    {
        var history = new[]
        {
            new ConversationTurn("user", "Hi, I have a question about refunds."),
            new ConversationTurn("assistant", "Sure, what would you like to know?"),
        };

        var prompt = _sut.BuildUserPrompt(context: [], history, question: "How long do refunds take?", ChannelProfile.Chat);

        var firstIndex = prompt.IndexOf("Hi, I have a question about refunds.", StringComparison.Ordinal);
        var secondIndex = prompt.IndexOf("Sure, what would you like to know?", StringComparison.Ordinal);
        firstIndex.Should().BeGreaterThan(-1);
        secondIndex.Should().BeGreaterThan(firstIndex);
    }

    [Fact]
    public void SystemPrompt_ForbidsOutsideKnowledgeAndInventedCitations()
    {
        var prompt = _sut.BuildSystemPrompt(ChannelProfile.Chat, tenantSystemPromptOverride: null);

        prompt.Should().Contain("only");
        prompt.Should().Contain(GroundedPromptAssembler.AbstentionMarker);
        prompt.ToLowerInvariant().Should().Contain("prior knowledge");   // outside knowledge forbidden
        prompt.ToLowerInvariant().Should().Contain("do not invent");     // no fabricated citations
    }
}
