using MediatR;

namespace NexConvo.Integrations.Application.Features.S3Config.Commands;

/// <summary>
/// Upserts workspace S3 storage configuration. One row per tenant.
/// Tenant is resolved server-side from JWT (Standard 6). <paramref name="ActorUserId"/> set from JWT (Standard 14).
/// <paramref name="AccessKeyId"/> and <paramref name="SecretAccessKey"/> are optional on update: empty keeps stored value.
/// </summary>
public sealed record SaveS3ConfigCommand(
    string BucketName,
    string Region,
    string? AccessKeyId,
    string? SecretAccessKey,
    string? CustomEndpoint,
    string? PathPrefix,
    bool IsActive,
    Guid ActorUserId) : IRequest<Guid>;
