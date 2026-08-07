namespace NexConvo.Integrations.Api.Models;

public sealed record SaveS3ConfigRequest(
    string BucketName,
    string Region,
    string? AccessKeyId,
    string? SecretAccessKey,
    string? CustomEndpoint,
    string? PathPrefix,
    bool IsActive);
