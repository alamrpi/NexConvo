namespace NexConvo.Chat.Application.Common.Interfaces;

public interface IKnowledgeRetrievalClient
{
    Task<IReadOnlyList<KnowledgeChunkMatch>> SearchAsync(
        string query, int topK, double minScore, CancellationToken cancellationToken);
}

public sealed record KnowledgeChunkMatch(string ChunkId, string DocumentId, string Content, double Score);