using MediatR;
using NexConvo.BuildingBlocks.Domain.Health;

namespace NexConvo.Integrations.Application.Features.S3Config.Commands;

/// <summary>
/// Live "Test Connection" probe (Standard 22: test-then-save) for a workspace's S3-compatible bucket.
/// When <see cref="AccessKeyId"/>/<see cref="SecretAccessKey"/> are blank, the handler falls back to the
/// tenant's stored, decrypted credentials so the user can re-test without re-entering secrets.
/// </summary>
public sealed record TestS3ConnectionCommand(
    string BucketName,
    string Region,
    string? AccessKeyId,
    string? SecretAccessKey,
    string? CustomEndpoint) : IRequest<ConnectionHealth>;
