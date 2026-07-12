using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Infrastructure.Multitenancy;

namespace NexConvo.Chat.Infrastructure.Persistence;

/// <summary>
/// Production implementation of <see cref="IChatDbContextFactory"/> — builds a fresh ChatDbContext
/// on the same connection string as the DI-scoped one, pinned to the given tenant via
/// FixedTenantContext, for consumers/jobs with no ambient HTTP tenant.
/// </summary>
public sealed class TenantChatDbContextFactory(IConfiguration configuration) : IChatDbContextFactory
{
    public IChatDbContext CreateForTenant(Guid tenantId)
    {
        var connectionString = configuration.GetConnectionString("ChatDb")
            ?? throw new InvalidOperationException("Connection string 'ChatDb' is not configured.");

        var optionsBuilder = new DbContextOptionsBuilder<ChatDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(3));

        return new ChatDbContext(optionsBuilder.Options, new FixedTenantContext(tenantId));
    }
}
