using System.Net;
using NexConvo.Identity.Application.Abstractions.Mailing;

namespace NexConvo.Identity.Application.Authentication;

/// <summary>
/// Composes the transactional auth emails. Interpolated values are HTML-encoded (XSS-safe).
/// English for v1; Bengali templates are a noted follow-up.
/// </summary>
public static class AuthEmails
{
    public static EmailMessage Verification(string toEmail, string name, string link) =>
        new(toEmail, name, "Verify your email",
            $"<p>Hi {Enc(name)},</p>" +
            "<p>Confirm your email address to finish setting up your NexConvo account:</p>" +
            $"<p><a href=\"{Enc(link)}\">Verify email</a></p>" +
            "<p>If you didn’t create this account, you can ignore this email.</p>");

    public static EmailMessage PasswordReset(string toEmail, string name, string link) =>
        new(toEmail, name, "Reset your password",
            $"<p>Hi {Enc(name)},</p>" +
            "<p>We received a request to reset your NexConvo password. This link expires in 30 minutes:</p>" +
            $"<p><a href=\"{Enc(link)}\">Reset password</a></p>" +
            "<p>If you didn’t request this, you can safely ignore this email.</p>");

    public static EmailMessage Invitation(string toEmail, string inviterName, string workspace, string link) =>
        new(toEmail, null, $"You’re invited to {workspace} on NexConvo",
            $"<p>{Enc(inviterName)} invited you to join the <strong>{Enc(workspace)}</strong> workspace on NexConvo.</p>" +
            $"<p><a href=\"{Enc(link)}\">Accept the invitation</a></p>");

    private static string Enc(string value) => WebUtility.HtmlEncode(value);
}
