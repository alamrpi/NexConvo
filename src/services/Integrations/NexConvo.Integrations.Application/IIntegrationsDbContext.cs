using Microsoft.EntityFrameworkCore;
using NexConvo.Integrations.Domain.Entities;

namespace NexConvo.Integrations.Application;

public interface IIntegrationsDbContext
{
    DbSet<WorkspaceAiConfig> WorkspaceAiConfigs { get; }
    DbSet<AuditLog> AuditLogs { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
