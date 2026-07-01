using NexConvo.Contracts.Enums;

namespace NexConvo.Integrations.Api.Models;

/// <summary>
/// Request body for upserting workspace AI config. The tenant and the acting user are resolved
/// server-side (JWT) — they are deliberately NOT part of this contract so a client can't forge them.
/// </summary>
public sealed record SaveAiConfigRequest(
    AiProviderType Provider,
    string? ApiKey,
    string? BaseUrl,
    string DefaultModel,
    string? Parameters,
    bool IsActive);
