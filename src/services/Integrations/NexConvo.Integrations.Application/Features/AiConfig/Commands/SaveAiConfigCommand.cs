using MediatR;
using NexConvo.Contracts.Enums;

namespace NexConvo.Integrations.Application.Features.AiConfig.Commands;

/// <summary>
/// Upserts the workspace AI provider configuration (one row per provider, keyed by tenant).
/// The tenant is resolved server-side from the JWT (ITenantContext) in the handler — never bound
/// from the client (skill Standard 6). <paramref name="ActorUserId"/> is the authenticated user,
/// set server-side from the JWT, and recorded as the audit "who" (skill Standard 14).
/// <paramref name="ApiKey"/> is optional on update: empty keeps the stored key.
/// </summary>
public sealed record SaveAiConfigCommand(
    AiProviderType Provider,
    string? ApiKey,
    string? BaseUrl,
    string DefaultModel,
    string? Parameters,
    bool IsActive,
    Guid ActorUserId) : IRequest<Guid>;
