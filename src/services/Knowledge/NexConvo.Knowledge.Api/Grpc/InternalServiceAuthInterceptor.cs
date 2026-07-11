using System.Security.Cryptography;
using System.Text;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Configuration;
using Serilog.Context;

namespace NexConvo.Knowledge.Api.Grpc;

/// <summary>
/// Gate for the internal-only <c>KnowledgeRetrieval</c> gRPC service (not exposed through the
/// public YARP gateway). Validates the same shared secret other internal-only endpoints already
/// use (<c>Internal:ApiKey</c> — see Identity's <c>InternalApiKeyMiddleware</c>) sent as gRPC
/// metadata — mTLS is deferred; this is the endpoint's AuthN (skill Standard 12) — then reads
/// tenant + correlation from metadata (never the request body, skill Standard 6) and pushes them
/// onto <see cref="GrpcCallContext"/> and the Serilog LogContext for the call, mirroring what
/// RequestCorrelationMiddleware does for HTTP requests (skill Standard 9).
/// </summary>
public sealed class InternalServiceAuthInterceptor(
    IConfiguration configuration,
    GrpcCallContext callContext) : Interceptor
{
    // gRPC metadata keys are always lowercase on the wire; mirrors Identity's "X-Internal-Api-Key".
    public const string InternalKeyHeader = "x-internal-api-key";
    public const string TenantIdHeader = "x-tenant-id";
    public const string TraceparentHeader = "traceparent";

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var configuredKey = configuration["Internal:ApiKey"];
        var providedKey = GetHeader(context.RequestHeaders, InternalKeyHeader);

        if (string.IsNullOrEmpty(configuredKey) ||
            string.IsNullOrEmpty(providedKey) ||
            !KeysMatch(configuredKey, providedKey))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid or missing internal service key."));
        }

        var tenantHeader = GetHeader(context.RequestHeaders, TenantIdHeader);
        if (!Guid.TryParse(tenantHeader, out var tenantId))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Missing or invalid tenant metadata."));
        }

        var correlationId = GetHeader(context.RequestHeaders, TraceparentHeader);
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N");
        }

        callContext.TenantId = tenantId;
        callContext.CorrelationId = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("TenantId", tenantId))
        using (LogContext.PushProperty("Service", "knowledge"))
        {
            return await continuation(request, context);
        }
    }

    private static string? GetHeader(Metadata headers, string key) =>
        headers.FirstOrDefault(e => e.Key == key)?.Value;

    /// <summary>Constant-time comparison to avoid leaking the secret via response-time side channels.</summary>
    private static bool KeysMatch(string configuredKey, string providedKey)
    {
        var configuredBytes = Encoding.UTF8.GetBytes(configuredKey);
        var providedBytes = Encoding.UTF8.GetBytes(providedKey);

        return configuredBytes.Length == providedBytes.Length
            && CryptographicOperations.FixedTimeEquals(configuredBytes, providedBytes);
    }
}
