using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexConvo.Identity.Api.Common;
using NexConvo.Identity.Application.Users;

namespace NexConvo.Identity.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
public sealed class UsersController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "users:read")]
    public async Task<IActionResult> List(
        CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        (await sender.Send(new ListUsersQuery(page, pageSize), cancellationToken)).ToActionResult();

    [HttpPut("{id:guid}/role")]
    [Authorize(Policy = "users:manage")]
    public async Task<IActionResult> ChangeRole(Guid id, [FromBody] ChangeRoleRequest body, CancellationToken cancellationToken) =>
        TryGetUserId(out var actor)
            ? (await sender.Send(new ChangeUserRoleCommand(id, body.RoleId, actor), cancellationToken)).ToActionResult()
            : Unauthorized();

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = "users:manage")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken) =>
        TryGetUserId(out var actor)
            ? (await sender.Send(new DeactivateUserCommand(id, actor), cancellationToken)).ToActionResult()
            : Unauthorized();

    [HttpPost("{id:guid}/reactivate")]
    [Authorize(Policy = "users:manage")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken cancellationToken) =>
        TryGetUserId(out var actor)
            ? (await sender.Send(new ReactivateUserCommand(id, actor), cancellationToken)).ToActionResult()
            : Unauthorized();

    private bool TryGetUserId(out Guid id)
    {
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out id);
    }
}

public sealed record ChangeRoleRequest(Guid RoleId);
