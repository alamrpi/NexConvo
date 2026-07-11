namespace NexConvo.Knowledge.Api.Grpc;

/// <summary>
/// Scoped holder populated by <see cref="InternalServiceAuthInterceptor"/> from gRPC metadata
/// (never a request body — skill Standard 6/12) and read by <see cref="GrpcTenantContext"/> and
/// the gRPC service class for structured logging. One instance per gRPC call (DI scope).
/// </summary>
public sealed class GrpcCallContext
{
    public Guid? TenantId { get; set; }
    public string? CorrelationId { get; set; }
}
