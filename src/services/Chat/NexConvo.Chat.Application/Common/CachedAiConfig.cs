namespace NexConvo.Chat.Application.Common;

/// <summary>
/// Shape of the JSON cached at <c>AiConfig:{TenantId}</c> in Redis by
/// <c>AiConfigUpdatedEventConsumer</c>. <see cref="EncryptedApiKey"/> is decrypted at use-time only
/// (never cached or logged in plaintext).
/// </summary>
public sealed record CachedAiConfig(
    Guid TenantId,
    string Provider,
    string EncryptedApiKey,
    string? BaseUrl,
    string DefaultModel,
    string? SystemPrompt,
    string? Parameters,
    bool IsActive);
