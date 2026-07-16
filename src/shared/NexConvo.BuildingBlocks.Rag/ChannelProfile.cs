namespace NexConvo.BuildingBlocks.Rag;

public enum RagRegister { Chat, Voice }

public enum StreamGranularity { Token, Sentence }

/// <summary>
/// Per-channel knobs for the shared grounding pipeline. Chat and Voice pass different
/// presets into the same <see cref="IGroundedPromptAssembler"/> — no channel-specific
/// branching lives in the assembler itself.
/// </summary>
public sealed record ChannelProfile(
    RagRegister Register,
    int TopK,
    double MinScore,
    double AnswerGateScore,
    int MaxAnswerTokens,
    bool EmitCitations,
    StreamGranularity StreamGranularity)
{
    public static readonly ChannelProfile Chat = new(
        RagRegister.Chat, TopK: 5, MinScore: 0.55, AnswerGateScore: 0.55, MaxAnswerTokens: 600,
        EmitCitations: true, StreamGranularity.Token);

    public static readonly ChannelProfile Voice = new(
        RagRegister.Voice, TopK: 3, MinScore: 0.6, AnswerGateScore: 0.6, MaxAnswerTokens: 80,
        EmitCitations: false, StreamGranularity.Sentence);
}
