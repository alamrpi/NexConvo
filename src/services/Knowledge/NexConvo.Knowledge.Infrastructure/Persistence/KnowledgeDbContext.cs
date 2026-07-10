using System.Reflection;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Domain;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.Knowledge.Application.Common.Interfaces;
using NexConvo.Knowledge.Domain.Entities;

namespace NexConvo.Knowledge.Infrastructure.Persistence;

/// <summary>
/// Knowledge service's own DbContext (database-per-service: skill Standard 5).
/// Applies RLS interceptor for tenant isolation (Standard 6) and UseVector()
/// for pgvector support (CHATBOT-ARCHITECTURE.md §12).
/// </summary>
public sealed class KnowledgeDbContext(
    DbContextOptions<KnowledgeDbContext> options,
    ITenantContext tenantContext)
    : DbContext(options), IKnowledgeDbContext
{
    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();
    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();
    public DbSet<KnowledgeAuditLog> KnowledgeAuditLogs => Set<KnowledgeAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // RLS interceptor sets app.current_tenant_id per connection (Standard 6)
        optionsBuilder.AddInterceptors(new RlsConnectionInterceptor(tenantContext));
        base.OnConfiguring(optionsBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Audit stamps (mirrors IdentityDbContext) — EF sends explicit values, so relying on the
        // column DEFAULT now() would silently insert 0001-01-01 timestamps.
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
            }

            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
