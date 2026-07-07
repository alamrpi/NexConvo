using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexConvo.Integrations.Api.Models;
using NexConvo.Integrations.Application.Features.S3Config.Commands;
using NexConvo.Integrations.Application.Features.S3Config.Queries;

namespace NexConvo.Integrations.Api.Controllers;

[ApiController]
[Route("api/v1/s3-config")]
[Authorize(Policy = "settings:manage")]
public sealed class WorkspaceS3ConfigController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(await sender.Send(new GetS3ConfigQuery(), ct));

    [HttpPut]
    public async Task<IActionResult> Save([FromBody] SaveS3ConfigRequest body, CancellationToken ct)
    {
        if (!TryGetUserId(out var actor)) return Unauthorized();

        var id = await sender.Send(
            new SaveS3ConfigCommand(
                body.BucketName,
                body.Region,
                body.AccessKeyId,
                body.SecretAccessKey,
                body.CustomEndpoint,
                body.PathPrefix,
                body.IsActive,
                actor),
            ct);

        return Ok(new { Id = id });
    }

    private bool TryGetUserId(out Guid id)
    {
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out id);
    }
}
