using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NexConvo.BuildingBlocks.Multitenancy;
using Serilog.Context;

namespace NexConvo.BuildingBlocks.Observability;

/// <summary>
/// Pushes <c>TenantId</c> and <c>UserId</c> onto the Serilog <see cref="LogContext"/> once per
/// request so every downstream log line is correlated without handlers re-passing them
/// (skill Standard 9). The trace id is added by the span enricher.
/// </summary>
public sealed class RequestCorrelationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var tenantId = context.User.FindFirst(HttpTenantContext.TenantClaimType)?.Value;
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? context.User.FindFirst("sub")?.Value;

        using (LogContext.PushProperty("TenantId", tenantId))
        using (LogContext.PushProperty("UserId", userId))
        {
            await next(context).ConfigureAwait(false);
        }
    }
}

public static class RequestCorrelationMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestCorrelation(this IApplicationBuilder app) =>
        app.UseMiddleware<RequestCorrelationMiddleware>();
}
