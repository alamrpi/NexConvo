using System.Runtime.CompilerServices;
using FluentAssertions;
using NexConvo.BuildingBlocks.Ai.Models;
using NexConvo.BuildingBlocks.Ai.Services;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.Contracts.Enums;
using NexConvo.Integrations.Application.Features.AiConfig;
using NexConvo.Integrations.Infrastructure.ExternalServices;
using NSubstitute;

namespace NexConvo.Integrations.Infrastructure.Tests;

public class AiConnectionTesterTests
{
    private static AiTestInput Input() => new(AiProviderType.OpenAI, "sk-test-key", null, "gpt-4o-mini");

    [Fact]
    public async Task Returns_healthy_on_successful_probe()
    {
        var provider = Substitute.For<IAiProviderService>();
        provider.GenerateStreamAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(OneChunk());

        var factory = Substitute.For<IAiProviderFactory>();
        factory.GetProvider(Arg.Any<AiProviderType>()).Returns(provider);

        var tester = new AiConnectionTester(factory);

        var result = await tester.TestAsync(Input(), default);

        result.Success.Should().BeTrue();
        result.Status.Should().Be(ConnectionStatus.Healthy);
        result.LatencyMs.Should().NotBeNull();
        result.Detail.Should().Be("Provider reachable");
    }

    [Fact]
    public async Task Returns_failed_on_http_exception_without_leaking_raw_message()
    {
        const string secretLeak = "raw upstream secret detail 12345";
        var provider = Substitute.For<IAiProviderService>();
        provider.GenerateStreamAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Throwing(new HttpRequestException(secretLeak)));

        var factory = Substitute.For<IAiProviderFactory>();
        factory.GetProvider(Arg.Any<AiProviderType>()).Returns(provider);

        var tester = new AiConnectionTester(factory);

        var result = await tester.TestAsync(Input(), default);

        result.Success.Should().BeFalse();
        result.Status.Should().Be(ConnectionStatus.Failed);
        string.IsNullOrWhiteSpace(result.ErrorMessage).Should().BeFalse();
        result.ErrorMessage.Should().NotContain(secretLeak);
    }

    private static async IAsyncEnumerable<AiStreamChunk> OneChunk()
    {
        yield return new AiStreamChunk("OK", "stop", 1, 1);
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<AiStreamChunk> Throwing(Exception exception)
    {
        await Task.Yield();
        throw exception;
#pragma warning disable CS0162 // unreachable yield — required so the compiler treats this as an iterator method
        yield return new AiStreamChunk("unreachable", null, null, null);
#pragma warning restore CS0162
    }
}
