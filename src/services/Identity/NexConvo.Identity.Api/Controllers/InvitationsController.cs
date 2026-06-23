using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexConvo.Identity.Api.Common;
using NexConvo.Identity.Application.Invitations;

namespace NexConvo.Identity.Api.Controllers;

[ApiController]
[Route("api/v1/invitations")]
public sealed class InvitationsController(ISender sender) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = "users:invite")]
    public async Task<IActionResult> Invite([FromBody] InviteRequest body, CancellationToken cancellationToken)
    {
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(sub, out var invitedBy))
        {
            return Unauthorized();
        }

        var invitedByName = User.FindFirst("name")?.Value ?? "A teammate";
        return (await sender.Send(
            new InviteUserCommand(body.Email, body.RoleName, invitedBy, invitedByName), cancellationToken))
            .ToActionResult();
    }

    [HttpGet]
    [Authorize(Policy = "users:invite")]
    public async Task<IActionResult> ListPending(CancellationToken cancellationToken) =>
        (await sender.Send(new ListPendingInvitationsQuery(), cancellationToken)).ToActionResult();

    /// <summary>Accept an invitation and provision the account. Anonymous; returns auth tokens.</summary>
    [HttpPost("accept")]
    [AllowAnonymous]
    public async Task<IActionResult> Accept([FromBody] AcceptInvitationRequest body, CancellationToken cancellationToken) =>
        (await sender.Send(
            new AcceptInvitationCommand(body.Token, body.FullName, body.Password), cancellationToken)).ToActionResult();
}

public sealed record InviteRequest(string Email, string RoleName);

public sealed record AcceptInvitationRequest(string Token, string FullName, string Password);
