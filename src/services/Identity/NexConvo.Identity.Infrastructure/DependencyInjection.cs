using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Infrastructure.Audit;
using NexConvo.Identity.Infrastructure.Common;
using NexConvo.Identity.Infrastructure.Multitenancy;
using NexConvo.Identity.Infrastructure.Persistence;
using NexConvo.Identity.Infrastructure.Security;

namespace NexConvo.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Tenancy: AmbientTenantContext (explicit-set OR JWT) replaces the default HttpTenantContext
        // so auth handlers can scope RLS before a JWT exists.
        services.AddHttpContextAccessor();
        services.AddScoped<AmbientTenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<AmbientTenantContext>());
        services.AddScoped<IAmbientTenantSetter>(sp => sp.GetRequiredService<AmbientTenantContext>());
        services.AddScoped<RlsConnectionInterceptor>();

        services.AddDbContext<IdentityDbContext>((sp, options) =>
            options.UseNpgsql(configuration.GetConnectionString("IdentityDb"))
                   .AddInterceptors(sp.GetRequiredService<RlsConnectionInterceptor>()));
        services.AddScoped<IIdentityDbContext>(sp => sp.GetRequiredService<IdentityDbContext>());

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<RsaKeyProvider>();
        services.AddSingleton<IJwtTokenIssuer, JwtTokenIssuer>();
        services.AddSingleton<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IAuditWriter, AuditWriter>();

        return services;
    }
}
