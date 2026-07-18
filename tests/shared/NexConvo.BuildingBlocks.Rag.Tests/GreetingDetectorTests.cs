using FluentAssertions;
using NexConvo.BuildingBlocks.Rag;
using Xunit;

namespace NexConvo.BuildingBlocks.Rag.Tests;

public class GreetingDetectorTests
{
    private readonly GreetingDetector _detector = new();

    [Theory]
    [InlineData("hi")]
    [InlineData("Hi")]
    [InlineData("hello")]
    [InlineData("hey")]
    [InlineData("hi there")]
    [InlineData("good morning")]
    [InlineData("thanks")]
    [InlineData("thank you")]
    [InlineData("bye")]
    [InlineData("  hi  ")]
    public void RecognizesGreetingsAndSmallTalk(string message) =>
        _detector.IsGreeting(message).Should().BeTrue();

    [Theory]
    [InlineData("How long do refunds take?")]
    [InlineData("What is the capital of France?")]
    [InlineData("hi, can you tell me about your refund policy?")]
    [InlineData("")]
    [InlineData("   ")]
    public void DoesNotFlagRealQuestionsAsGreetings(string message) =>
        _detector.IsGreeting(message).Should().BeFalse();
}
