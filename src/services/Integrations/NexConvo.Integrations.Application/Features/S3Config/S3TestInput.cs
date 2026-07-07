namespace NexConvo.Integrations.Application.Features.S3Config;

/// <summary>
/// Credential/target payload for a live "Test Connection" probe against a workspace's S3-compatible
/// bucket (Standard 22: test-then-save). Never persisted or logged as-is — secrets stay in memory
/// for the duration of the probe only.
/// </summary>
public sealed record S3TestInput(
    string BucketName,
    string Region,
    string AccessKeyId,
    string SecretAccessKey,
    string? CustomEndpoint);
