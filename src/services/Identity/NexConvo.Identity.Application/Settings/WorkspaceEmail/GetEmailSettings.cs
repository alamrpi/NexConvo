using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Domain.WorkspaceSettings;

namespace NexConvo.Identity.Application.Settings.WorkspaceEmail;

/// <summary>Email settings for the current workspace. The secret is never returned — only <see cref="HasSecret"/>.</summary>
public sealed record EmailSettingsDto(
    string Provider,
    string FromName,
    string FromAddress,
    bool IsEnabled,
    string? SmtpHost,
    int SmtpPort,
    string? SmtpUsername,
    bool SmtpUseSsl,
    bool HasSecret,
    DateTimeOffset? LastTestedAt,
    bool? LastTestSucceeded);

public sealed record GetEmailSettingsQuery : IRequest<Result<EmailSettingsDto>>;

public sealed class GetEmailSettingsQueryHandler(IIdentityDbContext db, ITenantContext tenant)
    : IRequestHandler<GetEmailSettingsQuery, Result<EmailSettingsDto>>
{
    public async Task<Result<EmailSettingsDto>> Handle(GetEmailSettingsQuery query, CancellationToken cancellationToken)
    {
        var settings = await db.WorkspaceEmailSettings
            .FirstOrDefaultAsync(x => x.TenantId == tenant.TenantId, cancellationToken);

        if (settings is null)
        {
            // Not configured yet — return sensible empty defaults so the form renders.
            return Result.Success(new EmailSettingsDto(
                nameof(EmailProvider.Smtp), string.Empty, string.Empty, false,
                null, 587, null, true, false, null, null));
        }

        return Result.Success(new EmailSettingsDto(
            settings.Provider.ToString(),
            settings.FromName,
            settings.FromAddress,
            settings.IsEnabled,
            settings.SmtpHost,
            settings.SmtpPort,
            settings.SmtpUsername,
            settings.SmtpUseSsl,
            settings.HasSecret,
            settings.LastTestedAt,
            settings.LastTestSucceeded));
    }
}
