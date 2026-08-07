using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.BuildingBlocks.Infrastructure.Web;

namespace NexConvo.BuildingBlocks.Tests.Web;

public sealed class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task ConnectionUnhealthyException_MapsTo409WithConnectionUnhealthyCode()
    {
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new ConnectionUnhealthyException("s3", "Access denied"),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        context.Response.ContentType.Should().Be("application/problem+json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var doc = await JsonDocument.ParseAsync(context.Response.Body);
        var root = doc.RootElement;

        root.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status409Conflict);
        root.GetProperty("code").GetString().Should().Be("connection-unhealthy");
        root.GetProperty("title").GetString().Should().Be("The s3 connection is not healthy. Access denied");
    }
}
