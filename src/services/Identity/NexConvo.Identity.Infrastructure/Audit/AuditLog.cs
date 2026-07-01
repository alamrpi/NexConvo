using NexConvo.BuildingBlocks.Domain;

namespace NexConvo.Identity.Infrastructure.Audit;

/// <summary>A tenant-scoped audit record (skill Standard 14). Stores identifiers only — never PII or secrets.</summary>
public sealed class AuditLog : ITenantEntity
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Action { get; private set; } = null!;
    public Guid? UserId { get; private set; }
    public string? Detail { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    private AuditLog() { } // EF

    public AuditLog(string action, Guid tenantId, Guid? userId, string? detail, DateTimeOffset occurredAt)
    {
        Action = action;
        TenantId = tenantId;
        UserId = userId;
        Detail = detail;
        OccurredAt = occurredAt;
    }
}
