using System.IO;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Resilience;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Application.Abstractions.Mailing;
using NexConvo.Identity.Infrastructure.Audit;
using NexConvo.Identity.Infrastructure.Common;
using NexConvo.Identity.Infrastructure.Mailing;
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
        services.AddSingleton<ILinkTokenService, LinkTokenService>();
        services.AddSingleton<IAppLinkBuilder, AppLinkBuilder>();
        services.AddScoped<IAuditWriter, AuditWriter>();

        // Secret-at-rest: Data Protection with a persisted key ring (filesystem in dev;
        // Key Vault in prod — follow-up). Without persistence, encrypted secrets break on restart.
        var keysDirectory = configuration["DataProtection:KeysDirectory"]
            ?? Path.Combine(AppContext.BaseDirectory, "dp-keys");
        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keysDirectory))
            .SetApplicationName("NexConvo.Identity");
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();

        // Email: platform-default options + Resend HTTP client (Polly) + per-tenant resolver.
        services.Configure<PlatformDefaultEmailOptions>(
            configuration.GetSection(PlatformDefaultEmailOptions.SectionName));
        services.AddHttpClient("resend", client => client.BaseAddress = new Uri("https://api.resend.com/"))
            .AddNexConvoResilience();
        services.AddScoped<ITenantEmailSenderResolver, TenantEmailSenderResolver>();

        return services;
    }
}
