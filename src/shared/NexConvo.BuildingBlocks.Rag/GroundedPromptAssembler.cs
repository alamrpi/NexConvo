using System.Text;

namespace NexConvo.BuildingBlocks.Rag;

public sealed class GroundedPromptAssembler : IGroundedPromptAssembler
{
    /// <summary>
    /// The literal token the model must emit verbatim to abstain. Shared with
    /// ReplyOrchestrator's abstention check — kept as a single constant so the instruction given
    /// to the model and the string the Chat side looks for can never drift apart.
    /// </summary>
    public const string AbstentionMarker = "[[NO_ANSWER]]";

    public string BuildSystemPrompt(ChannelProfile profile, string? tenantSystemPromptOverride)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a support assistant. Answer only from the numbered context provided below.");
        sb.AppendLine($"If the context does not contain the answer, respond with exactly {AbstentionMarker} and nothing else.");
        sb.AppendLine($"Never translate or localize {AbstentionMarker} — emit it verbatim in every language, even though your answer text itself should follow the instruction below.");
        sb.AppendLine("Always reply in the same language the user wrote in.");

        if (profile.EmitCitations)
        {
            sb.AppendLine("Cite the context you used inline with its number in brackets, e.g. [n].");
        }
        else
        {
            sb.AppendLine("Do not include citations, numbered lists, or links. Speak naturally, as in a phone call.");
            sb.AppendLine("Keep the answer to 1-2 sentences.");
        }

        if (!string.IsNullOrWhiteSpace(tenantSystemPromptOverride))
        {
            sb.AppendLine(tenantSystemPromptOverride);
        }

        return sb.ToString().TrimEnd();
    }

    public string BuildUserPrompt(
        IReadOnlyList<ContextContribution> context,
        IReadOnlyList<ConversationTurn> history,
        string question,
        ChannelProfile profile)
    {
        var sb = new StringBuilder();

        if (context.Count > 0)
        {
            sb.AppendLine("Context:");
            foreach (var c in context)
            {
                sb.AppendLine($"[{c.Index}] {c.Content}");
            }
            sb.AppendLine();
        }

        if (history.Count > 0)
        {
            sb.AppendLine("Conversation so far:");
            foreach (var turn in history)
            {
                sb.AppendLine($"{turn.Role}: {turn.Text}");
            }
            sb.AppendLine();
        }

        sb.AppendLine($"Question: {question}");

        return sb.ToString().TrimEnd();
    }
}
