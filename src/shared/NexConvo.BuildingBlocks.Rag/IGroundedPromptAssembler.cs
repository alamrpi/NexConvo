namespace NexConvo.BuildingBlocks.Rag;

/// <summary>
/// Assembles the final LLM prompt from retrieved context + history + the user's question,
/// driven entirely by a <see cref="ChannelProfile"/>. Pure logic — no I/O, no channel-specific
/// delivery. Shared by Chat today; Voice reuses it unchanged later with <see cref="ChannelProfile.Voice"/>.
/// </summary>
public interface IGroundedPromptAssembler
{
    string BuildSystemPrompt(ChannelProfile profile, string? tenantSystemPromptOverride);

    string BuildUserPrompt(
        IReadOnlyList<ContextContribution> context,
        IReadOnlyList<ConversationTurn> history,
        string question,
        ChannelProfile profile);
}
