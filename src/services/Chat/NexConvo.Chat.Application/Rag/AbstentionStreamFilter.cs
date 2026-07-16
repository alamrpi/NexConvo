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
        var released = false;

        await foreach (var chunk in source.WithCancellation(ct))
        {
            if (string.IsNullOrEmpty(chunk)) continue;

            if (released)
            {
                yield return new AbstentionResult(chunk, false);
                continue;
            }

            buffer.Append(chunk);
            var text = buffer.ToString();

            if (text.Contains(Marker, StringComparison.OrdinalIgnoreCase))
            {
                yield return new AbstentionResult(null, true);
                yield break;
            }

            // Once the buffer exceeds the window and can no longer be the start of the marker,
            // release it as a single token and stream freely thereafter.
            if (text.Length >= BufferWindow)
            {
                released = true;
                yield return new AbstentionResult(text, false);
                buffer.Clear();
            }
        }

        // Stream ended while still buffering (short answer, no marker) — flush what we held.
        if (!released && buffer.Length > 0)
        {
            yield return new AbstentionResult(buffer.ToString(), false);
        }
    }
}
