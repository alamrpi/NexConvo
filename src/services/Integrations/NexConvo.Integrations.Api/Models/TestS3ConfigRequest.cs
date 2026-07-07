namespace NexConvo.Integrations.Api.Models;

public sealed record TestS3ConfigRequest(
    string BucketName,
    string Region,
    string? AccessKeyId,
    string? SecretAccessKey,
    string? CustomEndpoint);
