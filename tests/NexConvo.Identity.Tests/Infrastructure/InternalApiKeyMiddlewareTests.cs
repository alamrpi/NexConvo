using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using NexConvo.Identity.Api.Middleware;

namespace NexConvo.Identity.Tests.Infrastructure;

/// <summary>
/// Guards <c>/internal/*</c> routes with a shared secret (X-Internal-Api-Key) instead of the
/// user-JWT pipeline — these routes are called service-to-service by Notification, never by a
/// logged-in user (Slice 6, Standard 12/13: internal key is env-only, never a user auth policy).
/// </summary>
public sealed class InternalApiKeyMiddlewareTests
{
    private const string ConfiguredKey = "test-shared-secret";

    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Internal:ApiKey"] = ConfiguredKey })
            .Build();

    private static (HttpContext context, bool nextCalled) Invoke(string path, string? headerValue, IConfiguration configuration)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        if (headerValue is not null)
        {
            context.Request.Headers["X-Internal-Api-Key"] = headerValue;
        }

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new InternalApiKeyMiddleware(next, configuration);
        middleware.InvokeAsync(context).GetAwaiter().GetResult();

        return (context, nextCalled);
    }

    [Fact]
    public void InvokeAsync_InternalPathWithoutHeader_Returns401AndDoesNotCallNext()
    {
        var (context, nextCalled) = Invoke("/internal/tenants/abc/health-alert-recipients", headerValue: null, BuildConfiguration());

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public void InvokeAsync_InternalPathWithWrongKey_Returns401AndDoesNotCallNext()
    {
        var (context, nextCalled) = Invoke("/internal/tenants/abc/health-alert-recipients", "wrong-key", BuildConfiguration());

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public void InvokeAsync_InternalPathWithCorrectKey_CallsNext()
    {
        var (_, nextCalled) = Invoke("/internal/tenants/abc/health-alert-recipients", ConfiguredKey, BuildConfiguration());

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public void InvokeAsync_NonInternalPath_CallsNextRegardlessOfHeader()
    {
        var (_, nextCalled) = Invoke("/api/v1/users", headerValue: null, BuildConfiguration());

        nextCalled.Should().BeTrue();
    }
}
