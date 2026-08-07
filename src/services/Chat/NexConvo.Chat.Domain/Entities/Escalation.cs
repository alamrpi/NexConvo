using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Domain.Entities;

/// <summary>
/// Records a single handoff request from AI to human handling for a <see cref="Conversation"/>.
/// Created only via <see cref="Conversation.RequestHandoff"/> — never directly.
/// </summary>
public class Escalation
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    public Guid ConversationId { get; private set; }
    public EscalationReason Reason { get; private set; }
    public DateTimeOffset RaisedAt { get; private set; } = DateTimeOffset.UtcNow;
    public Guid? AcceptedByUserId { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    private Escalation()
    {
        // EF Core
    }

    internal static Escalation Raise(Guid tenantId, Guid conversationId, EscalationReason reason) =>
        new() { TenantId = tenantId, ConversationId = conversationId, Reason = reason };

    public void Accept(Guid agentUserId)
    {
        AcceptedByUserId = agentUserId;
        AcceptedAt = DateTimeOffset.UtcNow;
    }

    public void Resolve() => ResolvedAt = DateTimeOffset.UtcNow;
}
