using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Application.Security;
using NexConvo.BuildingBlocks.Domain;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.Contracts.Events.Integrations;
using NexConvo.Integrations.Domain.Entities;

namespace NexConvo.Integrations.Application.Features.S3Config.Commands;

public sealed class SaveS3ConfigCommandHandler(
    IIntegrationsDbContext context,
    ITenantContext tenant,
    IAesEncryptionService encryptionService,
    IPublishEndpoint publishEndpoint,
    ILogger<SaveS3ConfigCommandHandler> logger)
    : IRequestHandler<SaveS3ConfigCommand, Guid>
{
    public async Task<Guid> Handle(SaveS3ConfigCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenant.TenantId;

        var config = await context.WorkspaceS3Configs
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

        var hasNewAccessKey = !string.IsNullOrWhiteSpace(request.AccessKeyId);
        var hasNewSecretKey = !string.IsNullOrWhiteSpace(request.SecretAccessKey);

        if (config is null)
        {
            if (!hasNewAccessKey || !hasNewSecretKey)
                throw new DomainException("Access Key ID and Secret Access Key are required when configuring S3 for the first time.");

            config = new WorkspaceS3Config(
                tenantId,
                request.BucketName,
                request.Region,
                encryptionService.Encrypt(request.AccessKeyId!),
                encryptionService.Encrypt(request.SecretAccessKey!),
                request.CustomEndpoint,
                request.PathPrefix,
                request.IsActive)
            {
                CreatedByUserId = request.ActorUserId,
            };

            context.WorkspaceS3Configs.Add(config);

            context.AuditLogs.Add(new AuditLog(
                "integrations.s3-config.create",
                tenantId,
                request.ActorUserId,
                $"bucket={request.BucketName};region={request.Region}",
                DateTimeOffset.UtcNow));
        }
        else
        {
            var encryptedAccessKeyId = hasNewAccessKey
                ? encryptionService.Encrypt(request.AccessKeyId!)
                : config.EncryptedAccessKeyId;

            var encryptedSecretKey = hasNewSecretKey
                ? encryptionService.Encrypt(request.SecretAccessKey!)
                : config.EncryptedSecretAccessKey;

            config.UpdateSettings(
                request.BucketName,
                request.Region,
                encryptedAccessKeyId,
                encryptedSecretKey,
                request.CustomEndpoint,
                request.PathPrefix);

            config.SetActive(request.IsActive);

            context.AuditLogs.Add(new AuditLog(
                "integrations.s3-config.update",
                tenantId,
                request.ActorUserId,
                $"bucket={request.BucketName};region={request.Region}",
                DateTimeOffset.UtcNow));
        }

        await context.SaveChangesAsync(cancellationToken);

        await publishEndpoint.Publish(
            new S3ConfigUpdatedEvent(
                config.TenantId,
                config.BucketName,
                config.Region,
                config.CustomEndpoint,
                config.PathPrefix,
                config.IsActive),
            cancellationToken);

        logger.LogInformation("Workspace S3 configuration {ConfigId} saved for bucket {Bucket}", config.Id, request.BucketName);

        return config.Id;
    }
}
