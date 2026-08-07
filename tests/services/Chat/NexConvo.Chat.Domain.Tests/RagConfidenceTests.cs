using FluentAssertions;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Chat.Domain.ValueObjects;
using Xunit;

namespace NexConvo.Chat.Domain.Tests;

public class RagConfidenceTests
{
    [Theory]
    [InlineData(0.85, ConfidenceBand.High)]
    [InlineData(0.6, ConfidenceBand.Medium)]
    [InlineData(0.2, ConfidenceBand.Low)]
    public void FromRetrievalAndAbstention_BandsByScoreWhenNotAbstained(double score, ConfidenceBand expectedBand)
    {
        var confidence = RagConfidence.FromRetrievalAndAbstention(score, modelAbstained: false);

        confidence.Score.Should().Be(score);
        confidence.Band.Should().Be(expectedBand);
    }

    [Fact]
    public void FromRetrievalAndAbstention_ModelAbstained_AlwaysLowRegardlessOfScore()
    {
        var confidence = RagConfidence.FromRetrievalAndAbstention(topRetrievalScore: 0.95, modelAbstained: true);

        confidence.Band.Should().Be(ConfidenceBand.Low);
    }
}
