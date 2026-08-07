using Microsoft.AspNetCore.Http;
using NexConvo.BuildingBlocks.Multitenancy;

namespace NexConvo.Knowledge.Api.Grpc;

/// <summary>
/// The single <see cref="ITenantContext"/> registration for this service, resolving correctly for
/// both request kinds: HTTP requests carry the tenant on the JWT <c>tenant_id</c> claim; gRPC
/// calls carry it on <see cref="GrpcCallContext"/>, populated by
/// <see cref="InternalServiceAuthInterceptor"/> from metadata (never the request body — skill
/// Standard 6). Every downstream consumer (KnowledgeDbContext's RLS interceptor, repositories,
/// handlers) keeps constructor-injecting <see cref="ITenantContext"/> exactly as before — this
/// class is the only thing that knows two call kinds exist.
/// </summary>
public sealed class CompositeTenantContext(
    IHttpContextAccessor httpContextAccessor,
    GrpcCallContext grpcCallContext) : ITenantContext
{
    public bool HasTenant => grpcCallContext.TenantId.HasValue || TryGetHttpTenant(out _);

    public Guid TenantId =>
        grpcCallContext.TenantId
        ?? (TryGetHttpTenant(out var tenantId)
            ? tenantId
            : throw new InvalidOperationException("No tenant in scope for the current HTTP or gRPC call."));

    private bool TryGetHttpTenant(out Guid tenantId)
    {
        var raw = httpContextAccessor.HttpContext?.User.FindFirst(HttpTenantContext.TenantClaimType)?.Value;
        return Guid.TryParse(raw, out tenantId);
    }
}
