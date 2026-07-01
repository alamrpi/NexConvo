using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NexConvo.Integrations.Infrastructure.Persistence;

public class IntegrationsDbContextFactory : IDesignTimeDbContextFactory<IntegrationsDbContext>
{
    public IntegrationsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IntegrationsDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=nexconvo_integrations;Username=postgres;Password=postgres");

        return new IntegrationsDbContext(optionsBuilder.Options);
    }
}
