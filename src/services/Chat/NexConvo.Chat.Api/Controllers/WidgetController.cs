using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Application.Features.Widget.Queries;

namespace NexConvo.Chat.Api.Controllers;

/// <summary>
/// Public widget configuration endpoint. Anonymous by design — the embeddable widget has no user
/// identity; it presents only its tenant's unguessable <c>WidgetToken</c>. The token is resolved to
/// a tenant via <see cref="IWidgetTenantResolver"/> (the single RLS-exempt lookup), then the config
/// is read through a tenant-scoped context so RLS is enforced (Standard 6). An unknown token returns
/// 404 — never another tenant's data, and never a signal that the token exists.
/// </summary>
[ApiController]
[Route("api/v1/widget")]
[AllowAnonymous] // Public widget: no user identity; tenant is proven by the unguessable WidgetToken (Standard 12).
[EnableCors("WidgetCorsPolicy")]
public sealed class WidgetController(ISender sender, IWidgetTenantResolver tenantResolver) : ControllerBase
{
    [HttpGet("config/{widgetToken:guid}")]
    public async Task<IActionResult> GetConfig(Guid widgetToken, CancellationToken ct)
    {
        var tenantId = await tenantResolver.ResolveAsync(widgetToken, ct);
        if (tenantId is null)
        {
            return NotFound();
        }

        var config = await sender.Send(new GetWidgetConfigurationQuery(tenantId.Value), ct);
        return config is null ? NotFound() : Ok(config);
    }
}
