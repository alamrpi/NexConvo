using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using NexConvo.Chat.Application.Features.Playground.Queries.GetProvidersAndModels;
using System.Collections.Generic;
namespace NexConvo.Chat.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class PlaygroundController : ControllerBase
{
    private readonly ISender _sender;

    public PlaygroundController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Gets the list of AI providers and models available for the Playground.
    /// </summary>
    [HttpGet("models")]
    [ProducesResponseType(typeof(IReadOnlyList<ProviderModelDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProvidersAndModels(CancellationToken cancellationToken)
    {
        var tenantId = User?.FindFirst("tenant_id")?.Value;
        var result = await _sender.Send(new GetProvidersAndModelsQuery(tenantId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }
}
