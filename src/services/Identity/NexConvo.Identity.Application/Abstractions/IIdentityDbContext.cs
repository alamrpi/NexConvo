using Microsoft.EntityFrameworkCore;
using NexConvo.Identity.Domain.Authentication;
using NexConvo.Identity.Domain.Roles;
using NexConvo.Identity.Domain.Tenants;
using NexConvo.Identity.Domain.Users;
using NexConvo.Identity.Domain.WorkspaceSettings;

namespace NexConvo.Identity.Application.Abstractions;

/// <summary>EF abstraction over the Identity database (skill Standard 1 — testable Application).</summary>
public interface IIdentityDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<WorkspaceEmailSettings> WorkspaceEmailSettings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
