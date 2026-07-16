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
    // AnswerGateScore measured live (Task 8) against a real tenant KB + Cohere embed-multilingual-v3
    // + real DeepSeek/OpenRouter answers: genuine in-KB questions scored 0.68-0.72; keyword-adjacent
    // but unanswerable questions topped out at 0.50; off-topic questions scored 0.18-0.29. 0.55-0.65
    // all cleanly separate the two clusters — set toward the stricter end (favor abstention) with
    // margin below the observed answer floor, per the plan's tie-breaking rule.
    public static readonly ChannelProfile Chat = new(
        RagRegister.Chat, TopK: 5, MinScore: 0.55, AnswerGateScore: 0.62, MaxAnswerTokens: 600,
        EmitCitations: true, StreamGranularity.Token);

    public static readonly ChannelProfile Voice = new(
        RagRegister.Voice, TopK: 3, MinScore: 0.6, AnswerGateScore: 0.65, MaxAnswerTokens: 80,
        EmitCitations: false, StreamGranularity.Sentence);
}
