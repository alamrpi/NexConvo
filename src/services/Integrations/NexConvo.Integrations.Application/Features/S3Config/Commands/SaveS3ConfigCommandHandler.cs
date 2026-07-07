using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Application.Health;
using NexConvo.BuildingBlocks.Application.Security;
using NexConvo.BuildingBlocks.Domain;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.Contracts.Events.Integrations;
using NexConvo.Integrations.Domain.Entities;

namespace NexConvo.Integrations.Application.Features.S3Config.Commands;

public sealed class SaveS3ConfigCommandHandler(
    IIntegrationsDbContext context,
    ITenantContext tenant,
    IAesEncryptionService encryptionService,
    IPublishEndpoint publishEndpoint,
    IConnectionTester<S3TestInput> tester,
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
        var isNewConfig = config is null;

        if (isNewConfig)
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
        }
        else
        {
            var encryptedAccessKeyId = hasNewAccessKey
                ? encryptionService.Encrypt(request.AccessKeyId!)
                : config!.EncryptedAccessKeyId;

            var encryptedSecretKey = hasNewSecretKey
                ? encryptionService.Encrypt(request.SecretAccessKey!)
                : config!.EncryptedSecretAccessKey;

            config!.UpdateSettings(
                request.BucketName,
                request.Region,
                encryptedAccessKeyId,
                encryptedSecretKey,
                request.CustomEndpoint,
                request.PathPrefix);

            config.SetActive(request.IsActive);
        }

        // Re-test credentials server-side whenever they're new/changed, before anything is
        // persisted. A failing probe throws (mapped to 422) and nothing is saved. Unchanged
        // credentials skip the re-test and leave prior health untouched (Task 9).
        if (hasNewAccessKey || hasNewSecretKey)
        {
            var accessKeyId = request.AccessKeyId ?? encryptionService.Decrypt(config.EncryptedAccessKeyId);
            var secretAccessKey = request.SecretAccessKey ?? encryptionService.Decrypt(config.EncryptedSecretAccessKey);

            var probe = await tester.TestAsync(
                new S3TestInput(request.BucketName, request.Region, accessKeyId, secretAccessKey, request.CustomEndpoint),
                cancellationToken);

            if (!probe.Success)
                throw new ConnectionTestFailedException("s3", probe.ErrorMessage);

            config.ApplyHealth(probe);
        }

        if (isNewConfig)
        {
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
