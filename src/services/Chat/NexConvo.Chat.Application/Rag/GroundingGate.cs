using NexConvo.BuildingBlocks.Rag;
using NexConvo.Chat.Application.Common.Interfaces;

namespace NexConvo.Chat.Application.Rag;

public sealed class GroundingGate : IGroundingGate
{
    public bool ShouldAnswer(IReadOnlyList<KnowledgeChunkMatch> matches, ChannelProfile profile) =>
        matches.Count > 0 && matches.Max(m => m.Score) >= profile.AnswerGateScore;
}
