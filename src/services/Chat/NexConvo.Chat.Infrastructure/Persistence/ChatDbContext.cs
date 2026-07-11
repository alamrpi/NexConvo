using System.Reflection;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Domain.Entities;

namespace NexConvo.Chat.Infrastructure.Persistence;

/// <summary>
/// Chat service's own DbContext (database-per-service: skill Standard 5).
/// Applies RLS interceptor for tenant isolation (Standard 6).
/// </summary>
public sealed class ChatDbContext(
    DbContextOptions<ChatDbContext> options,
    ITenantContext tenantContext)
    : DbContext(options), IChatDbContext
{
    public DbSet<ChannelConnection> ChannelConnections => Set<ChannelConnection>();
    public DbSet<WorkspaceChatSettings> WorkspaceChatSettings => Set<WorkspaceChatSettings>();
    public DbSet<ChatAuditLog> ChatAuditLogs => Set<ChatAuditLog>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Escalation> Escalations => Set<Escalation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // MassTransit's own EF outbox/inbox schema (Standard 10 — first use in the solution).
        // Infrastructure-internal transport-plumbing tables, intentionally NOT RLS-scoped —
        // tenant isolation is enforced by the business tables the outbox messages reference.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // RLS interceptor sets app.current_tenant_id per connection (Standard 6)
        optionsBuilder.AddInterceptors(new RlsConnectionInterceptor(tenantContext));
        base.OnConfiguring(optionsBuilder);
    }
}
