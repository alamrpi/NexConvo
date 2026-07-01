using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NexConvo.BuildingBlocks.Multitenancy;
using Serilog;
using Serilog.Context;

namespace NexConvo.BuildingBlocks.Observability;

/// <summary>
/// Establishes a request-spanning <c>CorrelationId</c> and pushes it — plus <c>TenantId</c> and
/// <c>UserId</c> — onto the Serilog <see cref="LogContext"/> so EVERY downstream log line is
/// correlated (skill Standard 9). The id is taken from the inbound <c>X-Correlation-ID</c> header
/// (set by the browser/BFF and forwarded by the gateway) so one id spans browser → BFF → gateway →
/// service; if absent it is generated. The id is echoed on the response. Place this AFTER
/// authentication so the tenant/user claims are populated.
/// </summary>
public sealed class RequestCorrelationMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > 128)
        {
            correlationId = Guid.NewGuid().ToString("N");
        }

        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId; // echo so callers can log/trace it

        var tenantId = context.User.FindFirst(HttpTenantContext.TenantClaimType)?.Value;
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? context.User.FindFirst("sub")?.Value;

        using (LogContext.PushProperty("CorrelationId", correlationId))
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

    /// <summary>
    /// Serilog request logging whose completion line is enriched with the CorrelationId (set by the
    /// inner <see cref="RequestCorrelationMiddleware"/>) plus the authenticated TenantId/UserId.
    /// Place this BEFORE authentication + correlation so it wraps them and the access log carries the id.
    /// </summary>
    public static IApplicationBuilder UseNexConvoRequestLogging(this IApplicationBuilder app) =>
        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnostic, context) =>
            {
                if (context.Items.TryGetValue(RequestCorrelationMiddleware.ItemKey, out var cid) && cid is string id)
                {
                    diagnostic.Set("CorrelationId", id);
                }

                var tenantId = context.User.FindFirst(HttpTenantContext.TenantClaimType)?.Value;
                var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? context.User.FindFirst("sub")?.Value;
                if (tenantId is not null) diagnostic.Set("TenantId", tenantId);
                if (userId is not null) diagnostic.Set("UserId", userId);
            };
        });
}
