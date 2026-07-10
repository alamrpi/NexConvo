using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NexConvo.BuildingBlocks.Multitenancy;

namespace NexConvo.Knowledge.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by EF Core tooling (dotnet ef migrations add/update).
/// Uses a NullTenantContext since migrations run outside any HTTP request scope.
/// </summary>
internal sealed class KnowledgeDbContextFactory : IDesignTimeDbContextFactory<KnowledgeDbContext>
{
    public KnowledgeDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=nexconvo_knowledge;Username=postgres;Password=postgres",
                npgsql => npgsql.UseVector())
            .Options;

        return new KnowledgeDbContext(options, new NullTenantContext());
    }
}

/// <summary>
/// A no-op ITenantContext for design-time tooling and migration runners that have no HTTP context.
/// </summary>
internal sealed class NullTenantContext : ITenantContext
{
    public bool HasTenant => false;
    public Guid TenantId => throw new InvalidOperationException(
        "No tenant context is available outside an HTTP request scope.");
}
