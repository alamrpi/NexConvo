using NexConvo.BuildingBlocks.Rag;
using NexConvo.Chat.Application.Common.Interfaces;

namespace NexConvo.Chat.Application.Rag;

/// <summary>
/// Decides whether retrieval is strong enough to let the LLM answer at all. If not, the caller must
/// NOT call the model — it returns the tenant's NoAnswerMessage (and hands off where a human exists).
/// This is the hard guarantee that the assistant never answers from outside the knowledge base.
/// </summary>
public interface IGroundingGate
{
    bool ShouldAnswer(IReadOnlyList<KnowledgeChunkMatch> matches, ChannelProfile profile);
}
