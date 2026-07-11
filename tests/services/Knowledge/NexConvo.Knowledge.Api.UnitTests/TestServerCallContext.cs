using System.Diagnostics;
using Grpc.Core;

namespace NexConvo.Knowledge.Api.UnitTests;

/// <summary>
/// Minimal <see cref="ServerCallContext"/> test double. ASP.NET Core gRPC ships no first-party
/// test helper for this abstract class (unlike the legacy Grpc.Core.Testing package), so
/// interceptor unit tests construct one directly with just the inbound metadata under test.
/// </summary>
internal sealed class TestServerCallContext(Metadata requestHeaders) : ServerCallContext
{
    protected override string MethodCore => "knowledge.KnowledgeRetrieval/Search";
    protected override string HostCore => "localhost";
    protected override string PeerCore => "test-peer";
    protected override DateTime DeadlineCore => DateTime.MaxValue;
    protected override Metadata RequestHeadersCore { get; } = requestHeaders;
    protected override CancellationToken CancellationTokenCore => CancellationToken.None;
    protected override Metadata ResponseTrailersCore { get; } = [];
    protected override Status StatusCore { get; set; }
    protected override WriteOptions? WriteOptionsCore { get; set; }
    protected override AuthContext AuthContextCore { get; } = new("test", new Dictionary<string, List<AuthProperty>>());

    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) =>
        throw new NotSupportedException();

    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
}
