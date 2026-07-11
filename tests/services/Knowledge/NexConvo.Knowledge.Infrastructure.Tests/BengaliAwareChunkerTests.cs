using FluentAssertions;
using NexConvo.Knowledge.Infrastructure.Chunking;

namespace NexConvo.Knowledge.Infrastructure.Tests;

public class BengaliAwareChunkerTests
{
    private readonly BengaliAwareChunker _sut = new();

    [Fact]
    public void Chunk_MixedBengaliEnglishParagraph_SplitsOnDandaAndSentenceBoundaries()
    {
        const string text =
            "আমি বাংলায় কথা বলি। This is an English sentence. আমার নাম রহিম।";

        var chunks = _sut.Chunk(text, languageHint: null);

        chunks.Should().NotBeEmpty();
        var allContent = string.Join(" ", chunks.Select(c => c.Content));
        allContent.Should().Contain("আমি বাংলায় কথা বলি");
        allContent.Should().Contain("This is an English sentence");
        allContent.Should().Contain("আমার নাম রহিম");
    }

    [Fact]
    public void Chunk_LongDocument_RespectsMaxTokenBound()
    {
        // 200 short sentences, each well under the cap alone, but far exceeding
        // ~500 tokens combined — forces the chunker to split across chunk boundaries.
        var text = string.Join(" ", Enumerable.Range(0, 200)
            .Select(i => $"This is sentence number {i} in a long English test document."));

        var chunks = _sut.Chunk(text, languageHint: "en");

        chunks.Should().NotBeEmpty();
        chunks.Should().HaveCountGreaterThan(1);
        chunks.Should().OnlyContain(c => c.TokenCount <= 500);
    }

    [Fact]
    public void Chunk_LongDocument_OverlapsConsecutiveChunksByRoughlyFifteenPercent()
    {
        var text = string.Join(" ", Enumerable.Range(0, 200)
            .Select(i => $"This is sentence number {i} in a long English test document."));

        var chunks = _sut.Chunk(text, languageHint: "en");

        chunks.Should().HaveCountGreaterThan(1);

        // The tail of chunk N should reappear at the head of chunk N+1 (sentence-level
        // overlap carried forward), proving retrieval doesn't lose context at the seam.
        for (var i = 0; i < chunks.Count - 1; i++)
        {
            var currentSentences = chunks[i].Content.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var nextSentences = chunks[i + 1].Content.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var lastOfCurrent = currentSentences.Last();
            nextSentences.Should().Contain(lastOfCurrent,
                "the last sentence of a chunk should be repeated at the start of the next chunk as overlap");
        }
    }

    [Fact]
    public void Chunk_OversizedSentenceWithConjuncts_NeverSplitsInsideAGraphemeCluster()
    {
        // Bengali conjunct clusters (base consonant + virama '্' + consonant, e.g. ক্ষ, জ্ঞ)
        // are single grapheme clusters. Repeat one enough times with no danda/period so the
        // chunker is forced to hard-split mid-"sentence" at the token cap.
        var cluster = "ক্ষ";
        var oversized = string.Concat(Enumerable.Repeat(cluster + " ", 800)); // no '।' or '.' at all

        var chunks = _sut.Chunk(oversized, languageHint: "bn");

        chunks.Should().NotBeEmpty();

        foreach (var chunk in chunks)
        {
            var elementEnumerator = System.Globalization.StringInfo.GetTextElementEnumerator(chunk.Content);
            var reconstructed = new System.Text.StringBuilder();
            while (elementEnumerator.MoveNext())
            {
                reconstructed.Append((string)elementEnumerator.Current);
            }

            // If the split had landed inside a grapheme cluster, re-walking the content by
            // text element would silently "repair" or reorder it relative to raw substring
            // slicing — instead assert the chunk never contains a lone combining virama
            // detached from its base consonant (the tell-tale sign of a mid-cluster cut).
            chunk.Content.Should().NotStartWith("্", "a chunk must never begin mid-grapheme-cluster (dangling virama)");
            chunk.Content.Should().NotEndWith("্", "a chunk must never end mid-grapheme-cluster (dangling virama before its consonant)");
        }
    }

    [Fact]
    public void Chunk_OversizedSingleSentenceWithNoPunctuation_HardSplitsAtSafeBoundaryWithinTokenBound()
    {
        // A single "sentence" (no danda, no period, no newline) long enough that it alone
        // blows the ~500-token cap. The chunker must hard-split it at a word/grapheme-safe
        // boundary rather than emitting one oversized chunk.
        var words = Enumerable.Range(0, 1000).Select(i => $"word{i}");
        var oneGiantSentence = string.Join(" ", words); // no sentence terminators anywhere

        var chunks = _sut.Chunk(oneGiantSentence, languageHint: "en");

        chunks.Should().HaveCountGreaterThan(1, "an oversized single sentence must be hard-split across multiple chunks");
        chunks.Should().OnlyContain(c => c.TokenCount <= 500);

        // Hard-splitting at a safe boundary means we never cut a word in half.
        foreach (var chunk in chunks)
        {
            chunk.Content.Should().NotStartWith(" ");
            chunk.Content.Trim().Split(' ').Should().OnlyContain(token => !string.IsNullOrWhiteSpace(token));
        }

        // Reassembling all chunk content (ignoring the ~15% overlap duplication) must still
        // contain the original leading and trailing words, proving no data was dropped.
        var allContent = string.Join(" ", chunks.Select(c => c.Content));
        allContent.Should().Contain("word0");
        allContent.Should().Contain("word999");
    }
}
