using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Application.Common;
using NexConvo.Identity.Domain.WorkspaceSettings;

namespace NexConvo.Identity.Application.Settings.WorkspaceEmail;

/// <summary><see cref="Secret"/> is the plaintext SMTP password / Resend API key; null/empty = keep existing.</summary>
public sealed record UpdateEmailSettingsCommand(
    string Provider,
    string FromName,
    string FromAddress,
    bool IsEnabled,
    string? SmtpHost,
    int? SmtpPort,
    string? SmtpUsername,
    bool? SmtpUseSsl,
    string? Secret,
    Guid ActorUserId) : IRequest<Result>, IRequireVerifiedActor;

public sealed class UpdateEmailSettingsCommandValidator : AbstractValidator<UpdateEmailSettingsCommand>
{
    public UpdateEmailSettingsCommandValidator()
    {
        RuleFor(x => x.Provider).Must(p => Enum.TryParse<EmailProvider>(p, out _))
            .WithMessage("Provider must be 'Smtp' or 'Resend'.");
        RuleFor(x => x.FromName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FromAddress).NotEmpty().EmailAddress();

        When(x => string.Equals(x.Provider, nameof(EmailProvider.Smtp), StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.SmtpHost).NotEmpty().WithMessage("SMTP host is required.");
            RuleFor(x => x.SmtpPort).NotNull().InclusiveBetween(1, 65535);
        });
    }
}

public sealed class UpdateEmailSettingsCommandHandler(
    IIdentityDbContext db,
    ITenantContext tenant,
    ISecretProtector secretProtector,
    IAuditWriter audit) : IRequestHandler<UpdateEmailSettingsCommand, Result>
{
    public async Task<Result> Handle(UpdateEmailSettingsCommand cmd, CancellationToken cancellationToken)
    {
        var provider = Enum.Parse<EmailProvider>(cmd.Provider, ignoreCase: true);

        var settings = await db.WorkspaceEmailSettings
            .FirstOrDefaultAsync(x => x.TenantId == tenant.TenantId, cancellationToken);
        var isNew = settings is null;
        settings ??= WorkspaceEmailSettings.CreateFor(tenant.TenantId);

        var encryptedSecret = string.IsNullOrEmpty(cmd.Secret) ? null : secretProtector.Protect(cmd.Secret);

        // Resend can't work without an API key; SMTP may be anonymous (e.g. Mailpit), so don't force one.
        if (cmd.IsEnabled && provider == EmailProvider.Resend && encryptedSecret is null && !settings.HasSecret)
        {
            return Result.Invalid("A Resend API key is required to enable email.");
        }

        if (provider == EmailProvider.Smtp)
        {
            settings.UpdateSmtp(
                cmd.FromName, cmd.FromAddress, cmd.SmtpHost!, cmd.SmtpPort ?? 587,
                cmd.SmtpUseSsl ?? true, cmd.SmtpUsername, encryptedSecret, cmd.IsEnabled);
        }
        else
        {
            settings.UpdateResend(cmd.FromName, cmd.FromAddress, encryptedSecret, cmd.IsEnabled);
        }

        if (isNew)
        {
            db.WorkspaceEmailSettings.Add(settings);
        }

        audit.Add("settings.email.update", tenant.TenantId, null, $"provider={provider};enabled={cmd.IsEnabled}");
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
