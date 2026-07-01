using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NexConvo.Identity.Infrastructure.Persistence;

/// <summary>
/// Lets the EF CLI build the context for migrations without the full host. Connects as the
/// privileged dev user (migrations create tables + enable RLS); the app runs as the
/// non-superuser <c>nexconvo_service</c>. Reads IDENTITY_DB_CONNECTION, with a localhost dev fallback.
/// </summary>
public sealed class DesignTimeIdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("IDENTITY_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=nexconvo_identity;Username=nexconvo;Password=localdev_pg_pw";

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(connection)
            .Options;

        return new IdentityDbContext(options);
    }
}
