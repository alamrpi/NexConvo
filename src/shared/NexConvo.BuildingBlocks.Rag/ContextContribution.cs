namespace NexConvo.BuildingBlocks.Rag;

/// <summary>One retrieved chunk, numbered for citation. RAG chunks today; MCP tool results later.</summary>
public sealed record ContextContribution(int Index, string ChunkId, string DocumentId, string Content, double Score);
