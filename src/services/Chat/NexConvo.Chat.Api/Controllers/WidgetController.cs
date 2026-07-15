using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using NexConvo.Chat.Application.Features.Widget.Queries;

namespace NexConvo.Chat.Api.Controllers;

[ApiController]
[Route("api/v1/widget")]
[EnableCors("WidgetCorsPolicy")]
public sealed class WidgetController(ISender sender) : ControllerBase
{
    [HttpGet("config/{tenantId:guid}")]
    public async Task<IActionResult> GetConfig(Guid tenantId, CancellationToken ct)
    {
        // Impersonate the tenant so RLS allows reading the settings
        var claims = new[] { new Claim("tenant_id", tenantId.ToString()) };
        HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "WidgetAuth"));

        var config = await sender.Send(new GetWidgetConfigurationQuery(tenantId), ct);
        if (config is null) return NotFound();
        
        return Ok(config);
    }
}
