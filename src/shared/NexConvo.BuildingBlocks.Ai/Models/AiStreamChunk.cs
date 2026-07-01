namespace NexConvo.BuildingBlocks.Ai.Models;

public record AiStreamChunk(
    string Content,
    string? Reason, // e.g. "stop", "length", null if still generating
    int? PromptTokens,
    int? CompletionTokens
);
