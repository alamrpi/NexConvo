using Microsoft.EntityFrameworkCore;
using NexConvo.Integrations.Application;
using NexConvo.Integrations.Domain.Entities;
using System.Reflection;

namespace NexConvo.Integrations.Infrastructure.Persistence;

public class IntegrationsDbContext : DbContext, IIntegrationsDbContext
{
    public IntegrationsDbContext(DbContextOptions<IntegrationsDbContext> options) : base(options)
    {
    }

    public DbSet<WorkspaceAiConfig> WorkspaceAiConfigs => Set<WorkspaceAiConfig>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }
}
