using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexConvo.Chat.Api.Extensions;
using NexConvo.Chat.Api.Models;
using NexConvo.Chat.Application.Features.ChannelConnections.Commands;
using NexConvo.Chat.Application.Features.ChannelConnections.Queries;
using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Api.Controllers;

[ApiController]
[Route("api/v1/channel-connections")]
[Authorize(Policy = "settings:manage")]
public sealed class ChannelConnectionsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await sender.Send(new GetChannelConnectionsQuery(), ct));

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SaveChannelConnectionRequest body, CancellationToken ct)
    {
        if (!TryGetUserId(out var actor)) return Unauthorized();

        var result = await sender.Send(
            new SaveChannelConnectionCommand(
                body.Channel,
                body.ExternalAccountId ?? string.Empty,
                body.AccountName,
                body.AccessToken,
                body.AppSecret,
                actor),
            ct);

        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var actor)) return Unauthorized();

        var result = await sender.Send(new DeleteChannelConnectionCommand(id, actor), ct);
        return result.ToActionResult();
    }

    [HttpPost("test")]
    public async Task<IActionResult> Test([FromBody] TestConnectionRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new TestChannelConnectionCommand(body.Channel, body.AccessToken, null),
            ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/test")]
    public async Task<IActionResult> TestById(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new TestChannelConnectionByIdCommand(id), ct));

    private bool TryGetUserId(out Guid id)
    {
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out id);
    }
}

public sealed record TestConnectionRequest(ChatChannel Channel, string AccessToken);
