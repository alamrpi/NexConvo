namespace NexConvo.Chat.Application.Rag;

/// <summary>One filtered stream item: either a token to forward, or an abstain signal.</summary>
public readonly record struct AbstentionResult(string? Token, bool Abstained);

/// <summary>
/// Wraps an LLM token stream and holds back the leading window until it is sure the reply is NOT an
/// abstention. If the abstention marker appears anywhere in the buffered window (even split across
/// chunks or prefaced by an apology), it yields a single Abstained result and stops — so the user
/// never sees the marker or a half-formed outside-KB answer. Otherwise it releases the buffer and
/// passes the rest through token-by-token.
/// </summary>
public interface IAbstentionStreamFilter
{
    IAsyncEnumerable<AbstentionResult> FilterAsync(IAsyncEnumerable<string> source, CancellationToken ct);
}
