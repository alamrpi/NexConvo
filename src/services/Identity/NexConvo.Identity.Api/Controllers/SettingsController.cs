using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexConvo.Identity.Api.Common;
using NexConvo.Identity.Application.Settings.WorkspaceEmail;

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
        (await sender.Send(
            new UpdateEmailSettingsCommand(
                body.Provider, body.FromName, body.FromAddress, body.IsEnabled,
                body.SmtpHost, body.SmtpPort, body.SmtpUsername, body.SmtpUseSsl, body.Secret),
            cancellationToken)).ToActionResult();

    [HttpPost("email/test")]
    public async Task<IActionResult> SendTest(CancellationToken cancellationToken)
    {
        var email = User.FindFirst("email")?.Value;
        return string.IsNullOrEmpty(email)
            ? Unauthorized()
            : (await sender.Send(new SendTestEmailCommand(email), cancellationToken)).ToActionResult();
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
