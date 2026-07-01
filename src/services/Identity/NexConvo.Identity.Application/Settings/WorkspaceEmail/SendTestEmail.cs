using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Application.Abstractions.Mailing;

namespace NexConvo.Identity.Application.Settings.WorkspaceEmail;

/// <summary>Sends a test email (to the requesting user) using the workspace's resolved sender.</summary>
public sealed record SendTestEmailCommand(string ToEmail) : IRequest<Result>;

public sealed class SendTestEmailCommandHandler(
    ITenantEmailSenderResolver senderResolver,
    IIdentityDbContext db,
    ITenantContext tenant,
    IAuditWriter audit,
    IClock clock) : IRequestHandler<SendTestEmailCommand, Result>
{
    private const string Subject = "NexConvo test email";
    private const string Body = "<p>Your NexConvo email configuration works. 🎉</p>";

    public async Task<Result> Handle(SendTestEmailCommand cmd, CancellationToken cancellationToken)
    {
        bool succeeded;
        string? error = null;
        try
        {
            var sender = await senderResolver.ResolveAsync(cancellationToken);
            await sender.SendAsync(new EmailMessage(cmd.ToEmail, null, Subject, Body), cancellationToken);
            succeeded = true;
        }
        catch (Exception ex)
        {
            succeeded = false;
            error = ex.Message;
        }

        var settings = await db.WorkspaceEmailSettings
            .FirstOrDefaultAsync(x => x.TenantId == tenant.TenantId, cancellationToken);
        settings?.RecordTestResult(succeeded, clock.UtcNow);

        audit.Add("settings.email.test", tenant.TenantId, null, succeeded ? "ok" : "failed");
        await db.SaveChangesAsync(cancellationToken);

        return succeeded ? Result.Success() : Result.Failure($"Failed to send test email: {error}");
    }
}
