namespace NexConvo.Chat.Domain.Enums;

/// <summary>
/// ToolCall/ToolResult are unused today but present now so a future MCP agentic loop
/// (P3) does not require a migration rewrite of the messages table's sender-type column.
/// </summary>
public enum MessageSenderRole { Contact, Ai, Agent, System, ToolCall, ToolResult }
