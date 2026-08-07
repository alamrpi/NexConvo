using NexConvo.Contracts.Enums;

namespace NexConvo.Integrations.Application.Features.AiConfig;

/// <summary>
/// Credential/target payload for a live "Test Connection" probe against an AI provider
/// (Standard 22: test-then-save). Never persisted or logged as-is — the API key stays in
/// memory for the duration of the probe only.
/// </summary>
public sealed record AiTestInput(AiProviderType Provider, string ApiKey, string? BaseUrl, string Model);
