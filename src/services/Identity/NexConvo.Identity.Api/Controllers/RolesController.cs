using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexConvo.Identity.Api.Common;
using NexConvo.Identity.Application.Roles;

namespace NexConvo.Identity.Api.Controllers;

[ApiController]
[Route("api/v1/roles")]
public sealed class RolesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "users:read")]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        (await sender.Send(new ListRolesQuery(), cancellationToken)).ToActionResult();

    [HttpGet("permissions")]
    [Authorize(Policy = "users:read")]
    public async Task<IActionResult> Catalog(CancellationToken cancellationToken) =>
        (await sender.Send(new GetPermissionCatalogQuery(), cancellationToken)).ToActionResult();

    [HttpPost]
    [Authorize(Policy = "roles:manage")]
    public async Task<IActionResult> Create([FromBody] RoleRequest body, CancellationToken cancellationToken) =>
        TryGetUserId(out var actor)
            ? (await sender.Send(new CreateRoleCommand(body.Name, body.Permissions, actor), cancellationToken)).ToActionResult()
            : Unauthorized();

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "roles:manage")]
    public async Task<IActionResult> Update(Guid id, [FromBody] RoleRequest body, CancellationToken cancellationToken) =>
        TryGetUserId(out var actor)
            ? (await sender.Send(new UpdateRoleCommand(id, body.Name, body.Permissions, actor), cancellationToken)).ToActionResult()
            : Unauthorized();

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "roles:manage")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        TryGetUserId(out var actor)
            ? (await sender.Send(new DeleteRoleCommand(id, actor), cancellationToken)).ToActionResult()
            : Unauthorized();

    private bool TryGetUserId(out Guid id)
    {
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out id);
    }
}

public sealed record RoleRequest(string Name, IReadOnlyList<string> Permissions);
