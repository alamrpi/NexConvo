using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Domain;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Domain.Authentication;
using NexConvo.Identity.Domain.Roles;
using NexConvo.Identity.Domain.Invitations;
using NexConvo.Identity.Domain.Tenants;
using NexConvo.Identity.Domain.Users;
using NexConvo.Identity.Domain.WorkspaceSettings;
using NexConvo.Identity.Infrastructure.Audit;

namespace NexConvo.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : DbContext(options), IIdentityDbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OneTimeToken> OneTimeTokens => Set<OneTimeToken>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<WorkspaceEmailSettings> WorkspaceEmailSettings => Set<WorkspaceEmailSettings>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
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
