using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexConvo.Identity.Api.Common;
using NexConvo.Identity.Application.Account;

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

    private bool TryGetUserId(out Guid id)
    {
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out id);
    }
}

public sealed record UpdateProfileRequest(string FullName);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
