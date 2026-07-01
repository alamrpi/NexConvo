using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Application.Abstractions.Mailing;
using NexConvo.Identity.Domain.WorkspaceSettings;

namespace NexConvo.Identity.Infrastructure.Mailing;

/// <summary>
/// Resolves the email sender for the current tenant: the workspace's own enabled provider
/// (secret decrypted on the fly) if configured, otherwise the platform default.
/// </summary>
public sealed class TenantEmailSenderResolver(
    IOptions<PlatformDefaultEmailOptions> platformOptions,
    IHttpClientFactory httpClientFactory,
    IIdentityDbContext db,
    ITenantContext tenant,
    ISecretProtector secretProtector) : ITenantEmailSenderResolver
{
    public async Task<IEmailSender> ResolveAsync(CancellationToken cancellationToken = default)
    {
        if (tenant.HasTenant)
        {
            var settings = await db.WorkspaceEmailSettings
                .FirstOrDefaultAsync(x => x.TenantId == tenant.TenantId, cancellationToken);

            if (settings is { IsEnabled: true })
            {
                return BuildFromWorkspace(settings);
            }
        }

        return BuildPlatformDefault();
    }

    private IEmailSender BuildFromWorkspace(WorkspaceEmailSettings settings)
    {
        var secret = settings.EncryptedSecret is null ? null : secretProtector.Unprotect(settings.EncryptedSecret);

        return settings.Provider switch
        {
            EmailProvider.Resend => new ResendEmailSender(
                httpClientFactory,
                new ResendEmailConfig(secret ?? string.Empty, settings.FromAddress, settings.FromName)),
            _ => new SmtpEmailSender(new SmtpEmailConfig(
                settings.SmtpHost ?? string.Empty, settings.SmtpPort, settings.SmtpUseSsl,
                settings.SmtpUsername, secret, settings.FromAddress, settings.FromName)),
        };
    }

    private IEmailSender BuildPlatformDefault()
    {
        var options = platformOptions.Value;
        return options.Provider switch
        {
            EmailProvider.Resend => new ResendEmailSender(
                httpClientFactory,
                new ResendEmailConfig(options.Resend.ApiKey, options.FromAddress, options.FromName)),
            _ => new SmtpEmailSender(new SmtpEmailConfig(
                options.Smtp.Host, options.Smtp.Port, options.Smtp.UseSsl,
                options.Smtp.Username, options.Smtp.Password, options.FromAddress, options.FromName)),
        };
    }
}
