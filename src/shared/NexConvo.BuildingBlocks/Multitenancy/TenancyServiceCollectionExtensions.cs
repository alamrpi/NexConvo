using Microsoft.Extensions.DependencyInjection;

namespace NexConvo.BuildingBlocks.Multitenancy;

public static class TenancyServiceCollectionExtensions
{
    /// <summary>
    /// Registers tenant resolution from the JWT and the RLS interceptor. Each service then
    /// attaches the interceptor to its own DbContext options.
    /// </summary>
    public static IServiceCollection AddNexConvoTenancy(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, HttpTenantContext>();
        services.AddScoped<RlsConnectionInterceptor>();
        return services;
    }
}
