using System.Globalization;
using System.Text;
using Microsoft.ML.Tokenizers;
using NexConvo.Knowledge.Application.Common.Interfaces;

namespace NexConvo.Knowledge.Infrastructure.Chunking;

/// <summary>
/// Splits extracted document text into ordered, token-bounded chunks, aware of Bengali (।)
/// and English (. ? newline) sentence boundaries. Pure in-memory logic — no I/O.
///
/// Algorithm:
///  1. Split the text into sentences on danda/period/question-mark/newline boundaries.
///  2. Any single sentence whose token count already exceeds the cap is itself hard-split
///     at word boundaries (never inside a Unicode grapheme cluster) into smaller pieces.
///  3. Sentences/pieces are greedily packed into chunks up to <see cref="MaxTokensPerChunk"/>.
///  4. Consecutive chunks share a trailing/leading overlap of roughly
///     <see cref="OverlapRatio"/> of the token budget, carried forward as whole sentences,
///     so retrieval doesn't lose context at a chunk seam.
/// </summary>
public sealed class BengaliAwareChunker : IChunker
{
    private const int MaxTokensPerChunk = 500;
    private const double OverlapRatio = 0.15;

    private static readonly char[] SentenceTerminators = ['।', '.', '?', '\n'];

    private readonly Tokenizer _tokenizer = TiktokenTokenizer.CreateForEncoding("cl100k_base");

    public IReadOnlyList<ChunkResult> Chunk(string text, string? languageHint)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var sentences = SplitIntoSentences(text);

        // Any oversized single sentence must be hard-split before packing so no chunk can
        // ever exceed the token cap regardless of how sentences are grouped.
        var pieces = new List<string>();
        foreach (var sentence in sentences)
        {
            pieces.AddRange(SplitOversizedSentence(sentence));
        }

        return PackIntoChunks(pieces);
    }

    private static List<string> SplitIntoSentences(string text)
    {
        var sentences = new List<string>();
        var start = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (Array.IndexOf(SentenceTerminators, text[i]) < 0)
            {
                continue;
            }

            var end = i + 1;
            var sentence = text[start..end].Trim();
            if (sentence.Length > 0)
            {
                sentences.Add(sentence);
            }

            start = end;
        }

        if (start < text.Length)
        {
            var remainder = text[start..].Trim();
            if (remainder.Length > 0)
            {
                sentences.Add(remainder);
            }
        }

        return sentences;
    }

    private List<string> SplitOversizedSentence(string sentence)
    {
        if (CountTokens(sentence) <= MaxTokensPerChunk)
        {
            return [sentence];
        }

        // Hard-split at word boundaries, never inside a grapheme cluster: walk the sentence
        // by Unicode text element (grapheme cluster), grouping whole "words" (whitespace-
        // delimited runs of grapheme clusters) until the token cap is hit.
        var words = SplitIntoGraphemeSafeWords(sentence);
        var result = new List<string>();
        var current = new StringBuilder();

        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (CountTokens(candidate) > MaxTokensPerChunk && current.Length > 0)
            {
                result.Add(current.ToString());
                current.Clear();
                current.Append(word);
            }
            else
            {
                current.Clear();
                current.Append(candidate);
            }
        }

        if (current.Length > 0)
        {
            result.Add(current.ToString());
        }

        return result;
    }

    private static List<string> SplitIntoGraphemeSafeWords(string sentence)
    {
        // Grapheme-cluster-safe tokenization: enumerate Unicode text elements (so we never
        // split a base consonant from a combining mark, e.g. Bengali virama conjuncts) and
        // group them into whitespace-delimited "words".
        var words = new List<string>();
        var current = new StringBuilder();

        var enumerator = StringInfo.GetTextElementEnumerator(sentence);
        while (enumerator.MoveNext())
        {
            var element = (string)enumerator.Current;
            if (element.Length == 1 && char.IsWhiteSpace(element[0]))
            {
                if (current.Length > 0)
                {
                    words.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(element);
            }
        }

        if (current.Length > 0)
        {
            words.Add(current.ToString());
        }

        return words;
    }

    private List<ChunkResult> PackIntoChunks(List<string> pieces)
    {
        var chunks = new List<ChunkResult>();
        var currentPieces = new List<string>();
        var currentTokens = 0;
        var ordinal = 0;

        foreach (var piece in pieces)
        {
            var pieceTokens = CountTokens(piece);

            if (currentPieces.Count > 0 && currentTokens + pieceTokens > MaxTokensPerChunk)
            {
                chunks.Add(BuildChunk(currentPieces, ordinal++));

                // Carry forward roughly the last OverlapRatio worth of tokens (whole
                // pieces) from the just-completed chunk as the start of the next one.
                currentPieces = TakeOverlapTail(currentPieces, CountTokens);
                currentTokens = currentPieces.Sum(CountTokens);
            }

            currentPieces.Add(piece);
            currentTokens += pieceTokens;
        }

        if (currentPieces.Count > 0)
        {
            chunks.Add(BuildChunk(currentPieces, ordinal));
        }

        return chunks;
    }

    private static List<string> TakeOverlapTail(List<string> pieces, Func<string, int> countTokens)
    {
        var overlapBudget = (int)(MaxTokensPerChunk * OverlapRatio);
        var tail = new List<string>();
        var tokens = 0;

        for (var i = pieces.Count - 1; i >= 0; i--)
        {
            var pieceTokens = countTokens(pieces[i]);

            // Never force an over-budget piece into the overlap just because the tail is
            // otherwise empty — a chunk-sized piece carried whole would blow the next
            // chunk's token cap before a single new piece is even added.
            if (tokens + pieceTokens > overlapBudget)
            {
                break;
            }

            tail.Insert(0, pieces[i]);
            tokens += pieceTokens;
        }

        return tail;
    }

    private ChunkResult BuildChunk(List<string> pieces, int ordinal)
    {
        var content = string.Join(" ", pieces);
        return new ChunkResult(content, ordinal, CountTokens(content));
    }

    private int CountTokens(string text) => _tokenizer.CountTokens(text);
}
