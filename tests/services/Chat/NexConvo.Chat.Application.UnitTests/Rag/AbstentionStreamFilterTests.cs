using FluentAssertions;
using NexConvo.Chat.Application.Rag;
using Xunit;

namespace NexConvo.Chat.Application.UnitTests.Rag;

public class AbstentionStreamFilterTests
{
    private readonly IAbstentionStreamFilter _filter = new AbstentionStreamFilter();

    private static async IAsyncEnumerable<string> Stream(params string[] chunks)
    {
        foreach (var c in chunks) { yield return c; await Task.Yield(); }
    }

    private async Task<(string text, bool abstained)> Collect(IAsyncEnumerable<string> src)
    {
        var sb = new System.Text.StringBuilder();
        var abstained = false;
        await foreach (var r in _filter.FilterAsync(src, default))
        {
            if (r.Abstained) { abstained = true; break; }
            if (r.Token is not null) sb.Append(r.Token);
        }
        return (sb.ToString(), abstained);
    }

    [Fact]
    public async Task NormalAnswer_PassesThroughUnchanged()
    {
        var (text, abstained) = await Collect(Stream("Refunds ", "take ", "5 days."));
        abstained.Should().BeFalse();
        text.Should().Be("Refunds take 5 days.");
    }

    [Fact]
    public async Task BareMarker_Abstains_NothingLeaks()
    {
        var (text, abstained) = await Collect(Stream("[[NO_ANSWER]]"));
        abstained.Should().BeTrue();
        text.Should().BeEmpty();
    }

    [Fact]
    public async Task MarkerSplitAcrossChunks_StillDetected()
    {
        var (text, abstained) = await Collect(Stream("[[NO_", "ANSWER", "]]"));
        abstained.Should().BeTrue();
        text.Should().BeEmpty();
    }

    [Fact]
    public async Task MarkerWrappedInText_Abstains()
    {
        // Model sometimes prefaces the marker; the leading buffer must catch it before release.
        var (text, abstained) = await Collect(Stream("I'm sorry, ", "[[NO_ANSWER]]"));
        abstained.Should().BeTrue();
        text.Should().BeEmpty();
    }

    [Fact]
    public async Task LongAnswerWithoutMarker_ReleasesAfterBuffer()
    {
        var (text, abstained) = await Collect(Stream(new string('a', 100)));
        abstained.Should().BeFalse();
        text.Should().Be(new string('a', 100));
    }

    [Fact]
    public async Task MarkerStraddlingTheReleaseBoundary_StillDetected_NothingLeaks()
    {
        // Regression test (audit fix): the marker begins right at the 64-char release window and
        // completes in the next chunk. The filter must not release the partial "[[NO_" prefix and
        // then stream the rest unchecked — it must retain enough tail to still catch the marker.
        var (text, abstained) = await Collect(Stream(new string('a', 60) + "[[NO_", "ANSWER]]"));
        abstained.Should().BeTrue();
        text.Should().NotContain("[[NO_");
        text.Should().NotContain("ANSWER");
    }

    [Fact]
    public async Task LongAnswerWithMarkerFarPastBuffer_StillDetected()
    {
        // A long grounded answer that later (incorrectly) contains the marker must still abstain,
        // even though most of its content already crossed the release boundary.
        var (text, abstained) = await Collect(Stream(new string('a', 200), "[[NO_ANSWER]]"));
        abstained.Should().BeTrue();
    }
}
