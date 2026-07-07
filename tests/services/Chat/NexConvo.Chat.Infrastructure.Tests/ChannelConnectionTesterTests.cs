using System.Net;
using System.Text;
using FluentAssertions;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.Chat.Application.Common;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Chat.Infrastructure.Services;
using NexConvo.Contracts.Enums;
using NSubstitute;

namespace NexConvo.Chat.Infrastructure.Tests;

public class ChannelConnectionTesterTests
{
    private const string SecretToken = "super-secret-access-token-12345";

    private static IChannelVerificationHttpClientFactory FakeFactory(HttpResponseMessage response)
    {
        var handler = new StubHttpMessageHandler(response);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var factory = Substitute.For<IChannelVerificationHttpClientFactory>();
        factory.CreateClient(Arg.Any<LeadSourceChannel>()).Returns(client);
        return factory;
    }

    [Fact]
    public async Task Returns_healthy_for_meta_channel_on_successful_probe()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"1","name":"Acme Page"}""", Encoding.UTF8, "application/json"),
        };
        var factory = FakeFactory(response);
        var tester = new ChannelConnectionTester(factory);

        var result = await tester.TestAsync(new ChannelTestInput(ChatChannel.Facebook, SecretToken, null), default);

        result.Success.Should().BeTrue();
        result.Status.Should().Be(ConnectionStatus.Healthy);
        result.LatencyMs.Should().NotBeNull();
        result.Detail.Should().Contain("Acme Page");
    }

    [Fact]
    public async Task Returns_healthy_for_telegram_channel_on_successful_probe()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"ok":true,"result":{"username":"mybot","first_name":"My Bot"}}""",
                Encoding.UTF8,
                "application/json"),
        };
        var factory = FakeFactory(response);
        var tester = new ChannelConnectionTester(factory);

        var result = await tester.TestAsync(new ChannelTestInput(ChatChannel.Telegram, SecretToken, null), default);

        result.Success.Should().BeTrue();
        result.Status.Should().Be(ConnectionStatus.Healthy);
        result.LatencyMs.Should().NotBeNull();
        result.Detail.Should().Contain("mybot");
    }

    [Fact]
    public async Task Returns_failed_without_leaking_token_or_body_on_unauthorized_response()
    {
        const string rawBody = "raw upstream body leak with secret detail 98765";
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(rawBody, Encoding.UTF8, "application/json"),
        };
        var factory = FakeFactory(response);
        var tester = new ChannelConnectionTester(factory);

        var result = await tester.TestAsync(new ChannelTestInput(ChatChannel.WhatsApp, SecretToken, null), default);

        result.Success.Should().BeFalse();
        result.Status.Should().Be(ConnectionStatus.Failed);
        string.IsNullOrWhiteSpace(result.ErrorMessage).Should().BeFalse();
        result.ErrorMessage.Should().NotContain(SecretToken);
        result.ErrorMessage.Should().NotContain(rawBody);
    }

    [Fact]
    public async Task Returns_healthy_for_web_channel_without_any_http_call()
    {
        var factory = Substitute.For<IChannelVerificationHttpClientFactory>();
        var tester = new ChannelConnectionTester(factory);

        var result = await tester.TestAsync(new ChannelTestInput(ChatChannel.Web, string.Empty, null), default);

        result.Success.Should().BeTrue();
        result.Status.Should().Be(ConnectionStatus.Healthy);
        factory.DidNotReceive().CreateClient(Arg.Any<LeadSourceChannel>());
    }

    [Fact]
    public async Task Returns_failed_for_unsupported_channel()
    {
        var factory = Substitute.For<IChannelVerificationHttpClientFactory>();
        var tester = new ChannelConnectionTester(factory);

        var result = await tester.TestAsync(new ChannelTestInput(ChatChannel.TikTok, SecretToken, null), default);

        result.Success.Should().BeFalse();
        result.Status.Should().Be(ConnectionStatus.Failed);
        string.IsNullOrWhiteSpace(result.ErrorMessage).Should().BeFalse();
    }

    private sealed class StubHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }
}
