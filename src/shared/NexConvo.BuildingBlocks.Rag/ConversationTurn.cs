namespace NexConvo.BuildingBlocks.Rag;

/// <summary>A prior turn in conversation history. Decoupled from any service's domain model — Role is "user" or "assistant".</summary>
public sealed record ConversationTurn(string Role, string Text);
