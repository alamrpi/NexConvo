using System.Net;

namespace NexConvo.Notification.Infrastructure.Tests;

/// <summary>Captures the single outgoing request and replies with a fixed response — used to
/// assert headers/body/URL without a real network call (mirrors ChannelConnectionTesterTests'
/// StubHttpMessageHandler, extended to capture the request for assertions).</summary>
internal sealed class CapturingHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
{
    public HttpRequestMessage? CapturedRequest { get; private set; }
    public string? CapturedBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CapturedRequest = request;
        CapturedBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        return response;
    }

    public static CapturingHttpMessageHandler WithOk(string jsonBody = "{}") => new(
        new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonBody),
        });
}
