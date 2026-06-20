using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Infrastructure.Persistence;

namespace NexConvo.Identity.Infrastructure.Audit;

/// <summary>
/// Stages an audit entry on the current DbContext so it commits atomically with the business
/// mutation in the handler's single SaveChanges (skill Standard 14).
/// </summary>
public sealed class AuditWriter(IdentityDbContext db, IClock clock) : IAuditWriter
{
    public void Add(string action, Guid tenantId, Guid? userId, string? detail) =>
        db.AuditLogs.Add(new AuditLog(action, tenantId, userId, detail, clock.UtcNow));
}
