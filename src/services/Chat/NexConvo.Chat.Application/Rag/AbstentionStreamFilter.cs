using System.Runtime.CompilerServices;
using System.Text;
using NexConvo.BuildingBlocks.Rag;

namespace NexConvo.Chat.Application.Rag;

public sealed class AbstentionStreamFilter : IAbstentionStreamFilter
{
    // Buffer enough to catch the marker even when models preface it (e.g. "I'm sorry, [[NO_ANSWER]]").
    private const int BufferWindow = 64;
    private static readonly string Marker = GroundedPromptAssembler.AbstentionMarker;

    public async IAsyncEnumerable<AbstentionResult> FilterAsync(
        IAsyncEnumerable<string> source, [EnumeratorCancellation] CancellationToken ct)
    {
        var buffer = new StringBuilder();

        await foreach (var chunk in source.WithCancellation(ct))
        {
            if (string.IsNullOrEmpty(chunk)) continue;

            buffer.Append(chunk);
            var text = buffer.ToString();

            if (text.Contains(Marker, StringComparison.OrdinalIgnoreCase))
            {
                yield return new AbstentionResult(null, true);
                yield break;
            }

            // Once the buffer exceeds the window, release everything except a trailing tail of
            // Marker.Length - 1 chars — the longest partial-marker prefix that could still be
            // completed by a future chunk (e.g. buffer ends "...[[NO_", next chunk "ANSWER]]").
            // Keeping that tail in the buffer means every subsequent chunk is re-scanned against
            // it, so a marker straddling a release boundary is still caught (audit fix — the
            // original release-and-stop design let such a split marker leak to the client).
            if (text.Length >= BufferWindow)
            {
                var keep = Math.Min(Marker.Length - 1, text.Length);
                var releasable = text[..^keep];
                if (releasable.Length > 0)
                {
                    yield return new AbstentionResult(releasable, false);
                }
                buffer.Clear();
                buffer.Append(text.AsSpan(text.Length - keep));
            }
        }

        // Stream ended while still buffering (short answer, no marker) — flush what we held.
        if (buffer.Length > 0)
        {
            yield return new AbstentionResult(buffer.ToString(), false);
        }
    }
}
