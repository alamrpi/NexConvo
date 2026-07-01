using NexConvo.Contracts.Enums;

namespace NexConvo.Integrations.Api.Models;

/// <summary>
/// Tests connectivity to an AI provider. ApiKey is optional — if empty, the stored (encrypted) key
/// for this provider is used. This allows testing an already-saved config without re-entering the key.
/// </summary>
public sealed record TestAiConnectionRequest(
    AiProviderType Provider,
    string? ApiKey,
    string? BaseUrl,
    string Model);
