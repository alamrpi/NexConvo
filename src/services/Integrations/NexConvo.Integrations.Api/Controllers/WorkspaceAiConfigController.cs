using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexConvo.Integrations.Api.Models;
using NexConvo.Integrations.Application.Features.AiConfig.Commands;
using NexConvo.Integrations.Application.Features.AiConfig.Queries;

namespace NexConvo.Integrations.Api.Controllers;

[ApiController]
[Route("api/v1/ai-config")]
[Authorize(Policy = "settings:manage")]
public sealed class WorkspaceAiConfigController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(await sender.Send(new GetAiConfigQuery(), ct));

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SaveAiConfigRequest body, CancellationToken ct)
    {
        if (!TryGetUserId(out var actor)) return Unauthorized();

        var id = await sender.Send(
            new SaveAiConfigCommand(
                body.Provider,
                body.ApiKey,
                body.BaseUrl,
                body.DefaultModel,
                body.Parameters,
                body.IsActive,
                actor),
            ct);

        return Ok(new { Id = id });
    }

    [HttpPost("test")]
    public async Task<IActionResult> Test([FromBody] TestAiConnectionRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new TestAiConnectionCommand(body.Provider, body.ApiKey, body.BaseUrl, body.Model), ct);

        return Ok(result);
    }

    private bool TryGetUserId(out Guid id)
    {
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out id);
    }
}
