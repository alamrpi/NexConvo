using NexConvo.BuildingBlocks.Domain;

namespace NexConvo.Knowledge.Domain.Entities;

/// <summary>Immutable audit record for every create/update/delete in the Knowledge service (Standard 14).</summary>
public sealed class KnowledgeAuditLog : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; set; }
    public Guid? UserId { get; private set; }
    public string Action { get; private set; }
    public string? Detail { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    private KnowledgeAuditLog() { Action = null!; }

    public KnowledgeAuditLog(string action, Guid tenantId, Guid? userId, string? detail, DateTimeOffset occurredAt)
    {
        Id = Guid.NewGuid();
        Action = action;
        TenantId = tenantId;
        UserId = userId;
        Detail = detail;
        OccurredAt = occurredAt;
    }
}
