using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexConvo.Identity.Application.Users;

namespace NexConvo.Identity.Api.Controllers;

/// <summary>
/// Service-to-service endpoints for internal callers only (Slice 6). NOT protected by the user-JWT
/// [Authorize] pipeline — guarded instead by <see cref="Middleware.InternalApiKeyMiddleware"/> on the
/// shared "/internal" path prefix, and must never be exposed via the public gateway (Standard 12).
///
/// [AllowAnonymous] reason: the deny-by-default fallback policy (Program.cs) requires an
/// authenticated JWT, but this endpoint's caller (Notification) never has a tenant user JWT — its
/// identity is instead proven by the X-Internal-Api-Key shared secret enforced upstream by
/// InternalApiKeyMiddleware. Anonymous here means "no JWT required", not "unguarded".
/// </summary>
[ApiController]
[Route("internal")]
[AllowAnonymous]
public sealed class InternalController(ISender sender) : ControllerBase
{
    /// <summary>Active Owner+Admin contacts for a tenant — used by Notification to route health alerts.</summary>
    [HttpGet("tenants/{tenantId:guid}/health-alert-recipients")]
    public async Task<IActionResult> GetHealthAlertRecipients(Guid tenantId, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetHealthAlertRecipientsQuery(tenantId), cancellationToken));
}
