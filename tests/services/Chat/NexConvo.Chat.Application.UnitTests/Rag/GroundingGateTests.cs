using FluentAssertions;
using NexConvo.BuildingBlocks.Rag;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Application.Rag;
using Xunit;

namespace NexConvo.Chat.Application.UnitTests.Rag;

public class GroundingGateTests
{
    private readonly IGroundingGate _gate = new GroundingGate();
    private static readonly ChannelProfile Profile =
        ChannelProfile.Chat with { AnswerGateScore = 0.65 };

    private static KnowledgeChunkMatch Match(double score) => new("c", "d", "content", score);

    [Fact]
    public void NoMatches_DoesNotAnswer() =>
        _gate.ShouldAnswer([], Profile).Should().BeFalse();

    [Fact]
    public void TopScoreBelowGate_DoesNotAnswer() =>
        _gate.ShouldAnswer([Match(0.60), Match(0.40)], Profile).Should().BeFalse();

    [Fact]
    public void TopScoreAtGate_Answers() =>
        _gate.ShouldAnswer([Match(0.65), Match(0.10)], Profile).Should().BeTrue();

    [Fact]
    public void TopScoreAboveGate_Answers() =>
        _gate.ShouldAnswer([Match(0.90)], Profile).Should().BeTrue();
}
