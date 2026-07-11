using FluentAssertions;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using NexConvo.Knowledge.Api.Grpc;
using NSubstitute;

namespace NexConvo.Knowledge.Api.UnitTests;

public class InternalServiceAuthInterceptorTests
{
    private const string ConfiguredKey = "test-shared-secret";

    private static InternalServiceAuthInterceptor CreateInterceptor(GrpcCallContext callContext)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new("Internal:ApiKey", ConfiguredKey)])
            .Build();
        return new InternalServiceAuthInterceptor(config, callContext);
    }

    private static ServerCallContext ContextWith(params (string key, string value)[] headers)
    {
        var metadata = new Metadata();
        foreach (var (key, value) in headers)
            metadata.Add(key, value);
        return new TestServerCallContext(metadata);
    }

    private static UnaryServerMethod<string, string> EchoHandler =>
        (request, _) => Task.FromResult(request);

    [Fact]
    public async Task MissingInternalKey_ThrowsUnauthenticated()
    {
        var callContext = new GrpcCallContext();
        var interceptor = CreateInterceptor(callContext);
        var context = ContextWith(("x-tenant-id", Guid.NewGuid().ToString()));

        var act = () => interceptor.UnaryServerHandler("req", context, EchoHandler);

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(StatusCode.Unauthenticated);
    }

    [Fact]
    public async Task WrongInternalKey_ThrowsUnauthenticated()
    {
        var callContext = new GrpcCallContext();
        var interceptor = CreateInterceptor(callContext);
        var context = ContextWith(
            ("x-internal-api-key", "wrong-key"),
            ("x-tenant-id", Guid.NewGuid().ToString()));

        var act = () => interceptor.UnaryServerHandler("req", context, EchoHandler);

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(StatusCode.Unauthenticated);
    }

    [Fact]
    public async Task MissingTenantId_ThrowsUnauthenticated()
    {
        var callContext = new GrpcCallContext();
        var interceptor = CreateInterceptor(callContext);
        var context = ContextWith(("x-internal-api-key", ConfiguredKey));

        var act = () => interceptor.UnaryServerHandler("req", context, EchoHandler);

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(StatusCode.Unauthenticated);
    }

    [Fact]
    public async Task ValidKeyAndTenant_PopulatesCallContext_AndInvokesHandler()
    {
        var callContext = new GrpcCallContext();
        var interceptor = CreateInterceptor(callContext);
        var tenantId = Guid.NewGuid();
        var context = ContextWith(
            ("x-internal-api-key", ConfiguredKey),
            ("x-tenant-id", tenantId.ToString()),
            ("traceparent", "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"));

        var response = await interceptor.UnaryServerHandler("req", context, EchoHandler);

        response.Should().Be("req");
        callContext.TenantId.Should().Be(tenantId);
        callContext.CorrelationId.Should().Be("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");
    }

    [Fact]
    public async Task ValidKeyAndTenant_NoTraceparent_GeneratesCorrelationId()
    {
        var callContext = new GrpcCallContext();
        var interceptor = CreateInterceptor(callContext);
        var context = ContextWith(
            ("x-internal-api-key", ConfiguredKey),
            ("x-tenant-id", Guid.NewGuid().ToString()));

        await interceptor.UnaryServerHandler("req", context, EchoHandler);

        callContext.CorrelationId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task InvalidTenantIdFormat_ThrowsUnauthenticated()
    {
        var callContext = new GrpcCallContext();
        var interceptor = CreateInterceptor(callContext);
        var context = ContextWith(
            ("x-internal-api-key", ConfiguredKey),
            ("x-tenant-id", "not-a-guid"));

        var act = () => interceptor.UnaryServerHandler("req", context, EchoHandler);

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(StatusCode.Unauthenticated);
    }
}
