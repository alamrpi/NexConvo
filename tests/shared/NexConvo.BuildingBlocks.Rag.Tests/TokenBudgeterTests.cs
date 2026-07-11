using FluentAssertions;
using NexConvo.BuildingBlocks.Rag;
using Xunit;

namespace NexConvo.BuildingBlocks.Rag.Tests;

public class TokenBudgeterTests
{
    private readonly TokenBudgeter _sut = new();

    [Fact]
    public void EstimateTokens_ReturnsPositiveCountForNonEmptyText()
    {
        _sut.EstimateTokens("Refunds are processed within 5 business days.").Should().BeGreaterThan(0);
    }

    [Fact]
    public void EstimateTokens_EmptyString_ReturnsZero()
    {
        _sut.EstimateTokens("").Should().Be(0);
    }

    [Fact]
    public void Fit_WithinBudget_ReturnsEverythingUnchanged()
    {
        var context = new[] { new ContextContribution(1, "c1", "d1", "Short answer.", 0.9) };
        var history = new[] { new ConversationTurn("user", "Hi") };

        var (fittedContext, fittedHistory) = _sut.Fit(context, history, maxContextTokens: 10_000);

        fittedContext.Should().BeEquivalentTo(context);
        fittedHistory.Should().BeEquivalentTo(history);
    }

    [Fact]
    public void Fit_OverBudget_TrimsOldestHistoryFirst()
    {
        var context = new[] { new ContextContribution(1, "c1", "d1", "Refunds take 5 business days to process once approved.", 0.9) };
        var history = new[]
        {
            new ConversationTurn("user", "This is an old message that should be trimmed first because it is oldest."),
            new ConversationTurn("assistant", "This is a newer reply that should be kept if possible."),
        };

        var (fittedContext, fittedHistory) = _sut.Fit(context, history, maxContextTokens: 20);

        fittedHistory.Should().NotContain(t => t.Text.StartsWith("This is an old message"));
        fittedContext.Should().NotBeEmpty();
    }

    [Fact]
    public void Fit_StillOverBudgetAfterTrimmingHistory_DropsLowestScoreContribution()
    {
        var context = new[]
        {
            new ContextContribution(1, "c1", "d1", "High score chunk with a decently long piece of explanatory text.", 0.95),
            new ContextContribution(2, "c2", "d2", "Low score chunk with a decently long piece of explanatory text.", 0.4),
        };

        var (fittedContext, _) = _sut.Fit(context, history: [], maxContextTokens: 15);

        fittedContext.Should().ContainSingle(c => c.ChunkId == "c1");
        fittedContext.Should().NotContain(c => c.ChunkId == "c2");
    }

    [Fact]
    public void Fit_NeverThrowsWhenEverythingIsTrimmedAway()
    {
        var context = new[] { new ContextContribution(1, "c1", "d1", "Some reasonably long chunk of text content here.", 0.9) };
        var history = new[] { new ConversationTurn("user", "Some reasonably long history message here too.") };

        var act = () => _sut.Fit(context, history, maxContextTokens: 0);

        act.Should().NotThrow();
    }
}
