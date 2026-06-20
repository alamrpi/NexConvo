using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Domain;

namespace NexConvo.BuildingBlocks.Infrastructure.Web;

/// <summary>
/// Maps unhandled exceptions to RFC 7807 problem responses: FluentValidation → 422,
/// DomainException → 400, everything else → 500 (logged once, with correlation via Serilog).
/// Never leaks internal detail on 500.
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            await WriteAsync(context, StatusCodes.Status422UnprocessableEntity, "Validation failed",
                ex.Errors.Select(e => e.ErrorMessage).Distinct().ToArray());
        }
        catch (DomainException ex)
        {
            await WriteAsync(context, StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception processing {Path}", context.Request.Path);
            await WriteAsync(context, StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    private static async Task WriteAsync(HttpContext context, int status, string title, string[]? errors = null)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var payload = JsonSerializer.Serialize(new
        {
            type = $"https://httpstatuses.io/{status}",
            title,
            status,
            errors,
        });

        await context.Response.WriteAsync(payload);
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseNexConvoExceptionHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<ExceptionHandlingMiddleware>();
}
