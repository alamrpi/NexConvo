using Microsoft.ML.Tokenizers;

namespace NexConvo.BuildingBlocks.Rag;

public sealed class TokenBudgeter : ITokenBudgeter
{
    private readonly Tokenizer _tokenizer = TiktokenTokenizer.CreateForEncoding("cl100k_base");

    public int EstimateTokens(string text) =>
        string.IsNullOrEmpty(text) ? 0 : _tokenizer.CountTokens(text);

    public (IReadOnlyList<ContextContribution> Context, IReadOnlyList<ConversationTurn> History) Fit(
        IReadOnlyList<ContextContribution> context,
        IReadOnlyList<ConversationTurn> history,
        int maxContextTokens)
    {
        var remainingHistory = new List<ConversationTurn>(history);
        var remainingContext = context.OrderByDescending(c => c.Score).ToList();

        while (TotalTokens(remainingContext, remainingHistory) > maxContextTokens && remainingHistory.Count > 0)
        {
            remainingHistory.RemoveAt(0); // oldest first
        }

        while (TotalTokens(remainingContext, remainingHistory) > maxContextTokens && remainingContext.Count > 0)
        {
            remainingContext.RemoveAt(remainingContext.Count - 1); // lowest score first (list is score-descending)
        }

        return (remainingContext, remainingHistory);
    }

    private int TotalTokens(IEnumerable<ContextContribution> context, IEnumerable<ConversationTurn> history) =>
        context.Sum(c => EstimateTokens(c.Content)) + history.Sum(h => EstimateTokens(h.Text));
}
