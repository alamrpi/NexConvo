using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Multitenancy;

namespace NexConvo.Integrations.Application.Features.S3Config.Queries;

public sealed class GetS3ConfigQueryHandler(
    IIntegrationsDbContext db,
    ITenantContext tenant,
    ILogger<GetS3ConfigQueryHandler> logger)
    : IRequestHandler<GetS3ConfigQuery, S3ConfigDto?>
{
    public async Task<S3ConfigDto?> Handle(GetS3ConfigQuery request, CancellationToken ct)
    {
        var config = await db.WorkspaceS3Configs
            .Where(x => x.TenantId == tenant.TenantId)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        if (config is null) return null;

        logger.LogInformation("Retrieved S3 config {ConfigId}", config.Id);

        // Never expose encrypted credentials — only signal whether they are set (Standard 13/15).
        return new S3ConfigDto(
            config.Id,
            config.BucketName,
            config.Region,
            !string.IsNullOrEmpty(config.EncryptedAccessKeyId),
            config.CustomEndpoint,
            config.PathPrefix,
            config.IsActive,
            config.LastTestStatus,
            config.LastTestedAt,
            config.LastTestError,
            config.LastTestLatencyMs
        );
    }
}
