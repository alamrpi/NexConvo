using FluentAssertions;
using Microsoft.Extensions.Options;
using NexConvo.Notification.Application.Abstractions.Mailing;
using NexConvo.Notification.Infrastructure.Mailing;
using NSubstitute;

namespace NexConvo.Notification.Infrastructure.Tests;

public class ResendEmailSenderTests
{
    [Fact]
    public async Task Sends_the_expected_payload_and_bearer_header()
    {
        var handler = CapturingHttpMessageHandler.WithOk();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") };
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("resend").Returns(httpClient);

        var options = Options.Create(new PlatformDefaultEmailOptions
        {
            FromAddress = "alerts@nexconvo.local",
            FromName = "NexConvo Alerts",
            Resend = new PlatformDefaultEmailOptions.ResendSection { ApiKey = "re_test_key_123" },
        });

        var sender = new ResendEmailSender(httpClientFactory, options);

        await sender.SendAsync(new EmailMessage("owner@tenant.test", "Owner", "[NexConvo] s3 connection is failing", "<p>body</p>"));

        var request = handler.CapturedRequest;
        request.Should().NotBeNull();
        request!.Method.Should().Be(HttpMethod.Post);
        request.RequestUri!.PathAndQuery.Should().Be("/emails");
        request.Headers.Authorization.Should().NotBeNull();
        request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        request.Headers.Authorization.Parameter.Should().Be("re_test_key_123");

        handler.CapturedBody.Should().NotBeNullOrWhiteSpace();
        handler.CapturedBody.Should().Contain("\"from\"");
        handler.CapturedBody.Should().Contain("owner@tenant.test");
        handler.CapturedBody.Should().Contain("\"subject\"");
        handler.CapturedBody.Should().Contain("s3 connection is failing");
        // JsonContent.Create HTML-escapes '<'/'>' by default (System.Text.Json web-safe encoding),
        // same as Identity's ResendEmailSender — assert on the escaped form it actually sends.
        handler.CapturedBody.Should().Contain("\"html\"");
        handler.CapturedBody.Should().Contain("body");
        handler.CapturedBody.Should().MatchRegex(@"\\u003Cp\\u003Ebody\\u003C/p\\u003E");
    }
}
