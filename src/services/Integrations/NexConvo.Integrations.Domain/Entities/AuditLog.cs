using NexConvo.BuildingBlocks.Domain;
using System;

namespace NexConvo.Integrations.Domain.Entities;

public sealed class AuditLog : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; set; }
    public Guid? UserId { get; private set; }
    public string Action { get; private set; }
    public string? Detail { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    private AuditLog()
    {
        Action = null!;
    }

    public AuditLog(string action, Guid tenantId, Guid? userId, string? detail, DateTimeOffset occurredAt)
    {
        Id = Guid.NewGuid();
        Action = action;
        TenantId = tenantId;
        UserId = userId;
        Detail = detail;
        OccurredAt = occurredAt;
    }
}
