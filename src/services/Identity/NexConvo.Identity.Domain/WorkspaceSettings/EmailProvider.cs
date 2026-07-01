namespace NexConvo.Identity.Domain.WorkspaceSettings;

/// <summary>The transactional email transport a workspace (or the platform default) uses.</summary>
public enum EmailProvider
{
    Smtp = 0,
    Resend = 1,
}
