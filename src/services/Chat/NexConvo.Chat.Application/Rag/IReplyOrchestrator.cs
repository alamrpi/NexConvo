namespace NexConvo.Chat.Application.Rag;

/// <summary>
/// Runs the grounded-reply pipeline for one inbound message on a conversation. Structured as a
/// single-iteration loop today; a future MCP tool-calling loop (P3) inserts additional iterations
/// here rather than requiring a rewrite. Cancelable end-to-end for a future Voice barge-in caller.
/// </summary>
public interface IReplyOrchestrator
{
    Task<ReplyOutcome> RunAsync(Guid conversationId, CancellationToken cancellationToken);
}
