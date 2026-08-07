using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NexConvo.BuildingBlocks.Ai.Services;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NexConvo.BuildingBlocks.Tests.Ai;

public class EmbeddingProviderHealthCheckTests
{
    private static EmbeddingProviderHealthCheck Build(CapturingJsonHandler handler, Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new EmbeddingProviderHealthCheck(new HttpClient(handler), configuration);
    }

    private static Task<HealthCheckResult> Check(EmbeddingProviderHealthCheck check)
        => check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

    [Fact]
    public async Task Bge_m3_default_is_healthy_when_server_responds_ok()
    {
        var handler = new CapturingJsonHandler("{}", HttpStatusCode.OK);
        var check = Build(handler, new Dictionary<string, string?> { ["EMBEDDING:BGEM3:BASEURL"] = "http://test-bge:7997" });

        var result = await Check(check);

        result.Status.Should().Be(HealthStatus.Healthy);
        handler.LastRequest!.RequestUri!.ToString().Should().Be("http://test-bge:7997/health");
    }

    [Fact]
    public async Task Bge_m3_is_unhealthy_when_server_errors()
    {
        var handler = new CapturingJsonHandler("{}", HttpStatusCode.ServiceUnavailable);
        var check = Build(handler, new Dictionary<string, string?> { ["EMBEDDING:BGEM3:BASEURL"] = "http://test-bge:7997" });

        var result = await Check(check);

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task Cohere_is_healthy_when_api_key_present()
    {
        var handler = new CapturingJsonHandler("{}");
        var check = Build(handler, new Dictionary<string, string?>
        {
            ["EMBEDDING:PROVIDER"] = "Cohere",
            ["EMBEDDING:COHERE:APIKEY"] = "some-key"
        });

        var result = await Check(check);

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Cohere_is_degraded_when_api_key_missing()
    {
        var handler = new CapturingJsonHandler("{}");
        var check = Build(handler, new Dictionary<string, string?> { ["EMBEDDING:PROVIDER"] = "Cohere" });

        var result = await Check(check);

        result.Status.Should().Be(HealthStatus.Degraded);
    }
}
