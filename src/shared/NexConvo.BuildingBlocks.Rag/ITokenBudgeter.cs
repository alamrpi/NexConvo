namespace NexConvo.BuildingBlocks.Rag;

/// <summary>Trims history and/or retrieved context so the assembled prompt fits the model's context window.</summary>
public interface ITokenBudgeter
{
    int EstimateTokens(string text);

    /// <summary>
    /// Trims oldest history turns first, then drops lowest-score contributions, until the
    /// combined estimated token count of context + history fits <paramref name="maxContextTokens"/>.
    /// Never mutates the inputs; returns new lists.
    /// </summary>
    (IReadOnlyList<ContextContribution> Context, IReadOnlyList<ConversationTurn> History) Fit(
        IReadOnlyList<ContextContribution> context,
        IReadOnlyList<ConversationTurn> history,
        int maxContextTokens);
}
