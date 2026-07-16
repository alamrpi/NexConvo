using FluentAssertions;
using NexConvo.BuildingBlocks.Rag;
using Xunit;

namespace NexConvo.BuildingBlocks.Rag.Tests;

public class ChannelProfileTests
{
    [Fact]
    public void Chat_HasAnswerGateAboveRetrievalMinScore()
    {
        // The answer gate must be at least as strict as retrieval — a chunk good enough to
        // retrieve is not automatically good enough to answer from.
        ChannelProfile.Chat.AnswerGateScore.Should().BeGreaterThanOrEqualTo(ChannelProfile.Chat.MinScore);
    }

    [Fact]
    public void Voice_HasAnswerGateAboveRetrievalMinScore()
    {
        ChannelProfile.Voice.AnswerGateScore.Should().BeGreaterThanOrEqualTo(ChannelProfile.Voice.MinScore);
    }
}
