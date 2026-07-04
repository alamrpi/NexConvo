using System.Reflection;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Domain.Entities;

namespace NexConvo.Chat.Infrastructure.Persistence;

/// <summary>
/// Chat service's own DbContext (database-per-service: skill Standard 5).
/// Applies RLS interceptor for tenant isolation (Standard 6) and UseVector()
/// for pgvector support (CHATBOT-ARCHITECTURE.md §12).
/// </summary>
public sealed class ChatDbContext(
    DbContextOptions<ChatDbContext> options,
    ITenantContext tenantContext)
    : DbContext(options), IChatDbContext
{
    public DbSet<ChannelConnection> ChannelConnections => Set<ChannelConnection>();
    public DbSet<WorkspaceChatSettings> WorkspaceChatSettings => Set<WorkspaceChatSettings>();
    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();
    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();
    public DbSet<ChatAuditLog> ChatAuditLogs => Set<ChatAuditLog>();

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
}
