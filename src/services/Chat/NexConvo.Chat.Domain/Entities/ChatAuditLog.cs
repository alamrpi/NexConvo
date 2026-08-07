using NexConvo.BuildingBlocks.Domain;

namespace NexConvo.Chat.Domain.Entities;

/// <summary>Immutable audit record for every create/update/delete in the Chat service (Standard 14).</summary>
public sealed class ChatAuditLog : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; set; }
    public Guid? UserId { get; private set; }
    public string Action { get; private set; }
    public string? Detail { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    private ChatAuditLog() { Action = null!; }

    public ChatAuditLog(string action, Guid tenantId, Guid? userId, string? detail, DateTimeOffset occurredAt)
    {
        Id = Guid.NewGuid();
        Action = action;
        TenantId = tenantId;
        UserId = userId;
        Detail = detail;
        OccurredAt = occurredAt;
    }
}
