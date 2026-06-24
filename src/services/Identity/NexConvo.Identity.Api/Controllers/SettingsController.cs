using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexConvo.Identity.Api.Common;
using NexConvo.Identity.Application.Settings.WorkspaceEmail;
using NexConvo.Identity.Application.Settings.WorkspaceSecurity;

namespace NexConvo.Identity.Api.Controllers;

[ApiController]
[Route("api/v1/settings")]
[Authorize(Policy = "settings:manage")] // deny-by-default RBAC (skill Standard 12)
public sealed class SettingsController(ISender sender) : ControllerBase
{
    [HttpGet("email")]
    public async Task<IActionResult> GetEmail(CancellationToken cancellationToken) =>
        (await sender.Send(new GetEmailSettingsQuery(), cancellationToken)).ToActionResult();

    [HttpPut("email")]
    public async Task<IActionResult> UpdateEmail([FromBody] UpdateEmailRequest body, CancellationToken cancellationToken) =>
        TryGetUserId(out var actor)
            ? (await sender.Send(
                new UpdateEmailSettingsCommand(
                    body.Provider, body.FromName, body.FromAddress, body.IsEnabled,
                    body.SmtpHost, body.SmtpPort, body.SmtpUsername, body.SmtpUseSsl, body.Secret, actor),
                cancellationToken)).ToActionResult()
            : Unauthorized();

    [HttpPost("email/test")]
    public async Task<IActionResult> SendTest(CancellationToken cancellationToken)
    {
        var email = User.FindFirst("email")?.Value;
        return string.IsNullOrEmpty(email)
            ? Unauthorized()
            : (await sender.Send(new SendTestEmailCommand(email), cancellationToken)).ToActionResult();
    }

    [HttpGet("security")]
    public async Task<IActionResult> GetSecurity(CancellationToken cancellationToken) =>
        (await sender.Send(new GetSecuritySettingsQuery(), cancellationToken)).ToActionResult();

    [HttpPut("security")]
    public async Task<IActionResult> UpdateSecurity([FromBody] UpdateSecurityRequest body, CancellationToken cancellationToken) =>
        TryGetUserId(out var actor)
            ? (await sender.Send(new UpdateSecuritySettingsCommand(body.RequireTwoFactor, actor), cancellationToken)).ToActionResult()
            : Unauthorized();

    private bool TryGetUserId(out Guid id)
    {
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out id);
    }
}

public sealed record UpdateEmailRequest(
    string Provider,
    string FromName,
    string FromAddress,
    bool IsEnabled,
    string? SmtpHost,
    int? SmtpPort,
    string? SmtpUsername,
    bool? SmtpUseSsl,
    string? Secret);

public sealed record UpdateSecurityRequest(bool RequireTwoFactor);
