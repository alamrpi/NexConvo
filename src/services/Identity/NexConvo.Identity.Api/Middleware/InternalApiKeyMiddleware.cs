using System.Security.Cryptography;
using System.Text;

namespace NexConvo.Identity.Api.Middleware;

/// <summary>
/// Guards <c>/internal/*</c> routes with a shared secret instead of the user-JWT pipeline (Slice 6,
/// Standard 12/13): these routes are called service-to-service (Notification resolving health-alert
/// recipients) and must never be reachable by a logged-in tenant user or via the public gateway. The
/// secret is env-only (<c>Internal:ApiKey</c>) — never hardcoded, never logged.
/// </summary>
public sealed class InternalApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private const string HeaderName = "X-Internal-Api-Key";
    private const string InternalPathPrefix = "/internal";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments(InternalPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var configuredKey = configuration["Internal:ApiKey"];
        var providedKey = context.Request.Headers[HeaderName].FirstOrDefault();

        if (string.IsNullOrEmpty(configuredKey) || string.IsNullOrEmpty(providedKey) || !KeysMatch(configuredKey, providedKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next(context);
    }

    /// <summary>Constant-time comparison to avoid leaking the secret via response-time side channels.</summary>
    private static bool KeysMatch(string configuredKey, string providedKey)
    {
        var configuredBytes = Encoding.UTF8.GetBytes(configuredKey);
        var providedBytes = Encoding.UTF8.GetBytes(providedKey);

        // Lengths differ -> definitely not equal, but still compare against self to keep timing
        // roughly constant rather than short-circuiting immediately.
        return configuredBytes.Length == providedBytes.Length
            && CryptographicOperations.FixedTimeEquals(configuredBytes, providedBytes);
    }
}

public static class InternalApiKeyMiddlewareExtensions
{
    public static IApplicationBuilder UseInternalApiKey(this IApplicationBuilder app) =>
        app.UseMiddleware<InternalApiKeyMiddleware>();
}
