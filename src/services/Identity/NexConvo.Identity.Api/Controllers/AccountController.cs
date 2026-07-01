using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexConvo.Identity.Api.Common;
using NexConvo.Identity.Application.Account;
using NexConvo.Identity.Application.Account.TwoFactor;

namespace NexConvo.Identity.Api.Controllers;

[ApiController]
[Route("api/v1/account")]
[Authorize] // self-service: the acting user is the JWT subject
public sealed class AccountController(ISender sender) : ControllerBase
{
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest body, CancellationToken cancellationToken) =>
        TryGetUserId(out var id)
            ? (await sender.Send(new UpdateProfileCommand(id, body.FullName), cancellationToken)).ToActionResult()
            : Unauthorized();

    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest body, CancellationToken cancellationToken) =>
        TryGetUserId(out var id)
            ? (await sender.Send(new ChangePasswordCommand(id, body.CurrentPassword, body.NewPassword), cancellationToken)).ToActionResult()
            : Unauthorized();

    [HttpPost("2fa/start")]
    public async Task<IActionResult> StartTwoFactor(CancellationToken cancellationToken) =>
        TryGetUserId(out var id)
            ? (await sender.Send(new StartTotpEnrollmentCommand(id), cancellationToken)).ToActionResult()
            : Unauthorized();

    [HttpPost("2fa/confirm")]
    public async Task<IActionResult> ConfirmTwoFactor([FromBody] ConfirmTwoFactorRequest body, CancellationToken cancellationToken) =>
        TryGetUserId(out var id)
            ? (await sender.Send(new ConfirmTotpEnrollmentCommand(id, body.Code), cancellationToken)).ToActionResult()
            : Unauthorized();

    [HttpPost("2fa/disable")]
    public async Task<IActionResult> DisableTwoFactor([FromBody] DisableTwoFactorRequest body, CancellationToken cancellationToken) =>
        TryGetUserId(out var id)
            ? (await sender.Send(new DisableTotpCommand(id, body.Password), cancellationToken)).ToActionResult()
            : Unauthorized();

    private bool TryGetUserId(out Guid id)
    {
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out id);
    }
}

public sealed record UpdateProfileRequest(string FullName);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record ConfirmTwoFactorRequest(string Code);

public sealed record DisableTwoFactorRequest(string Password);
