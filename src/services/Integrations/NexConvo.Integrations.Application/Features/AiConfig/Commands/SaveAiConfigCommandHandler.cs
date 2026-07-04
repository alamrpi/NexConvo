using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Application.Security;
using NexConvo.BuildingBlocks.Domain;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.Contracts.Events.Integrations;
using NexConvo.Integrations.Domain.Entities;

namespace NexConvo.Integrations.Application.Features.AiConfig.Commands;

public sealed class SaveAiConfigCommandHandler(
    IIntegrationsDbContext context,
    ITenantContext tenant,
    IAesEncryptionService encryptionService,
    IPublishEndpoint publishEndpoint,
    ILogger<SaveAiConfigCommandHandler> logger)
    : IRequestHandler<SaveAiConfigCommand, Guid>
{
    public async Task<Guid> Handle(SaveAiConfigCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenant.TenantId;

        var config = await context.WorkspaceAiConfigs
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Provider == request.Provider, cancellationToken);

        var hasNewKey = !string.IsNullOrWhiteSpace(request.ApiKey);
        var parameters = string.IsNullOrWhiteSpace(request.Parameters) ? null : request.Parameters;

        if (config is null)
        {
            if (!hasNewKey)
                throw new DomainException("An API key is required when configuring a new provider.");

            config = new WorkspaceAiConfig(
                tenantId,
                request.Provider,
                encryptionService.Encrypt(request.ApiKey!),
                request.BaseUrl,
                request.DefaultModel,
                null,
                parameters,
                request.IsActive)
            {
                CreatedByUserId = request.ActorUserId,
            };

            context.WorkspaceAiConfigs.Add(config);

            context.AuditLogs.Add(new AuditLog(
                "integrations.ai-config.create",
                tenantId,
                request.ActorUserId,
                $"provider={request.Provider};model={request.DefaultModel}",
                DateTimeOffset.UtcNow));
        }
        else
        {
            var encryptedKey = hasNewKey ? encryptionService.Encrypt(request.ApiKey!) : config.EncryptedApiKey;

            config.UpdateSettings(
                encryptedKey,
                request.BaseUrl,
                request.DefaultModel,
                null,
                parameters);

            config.SetActive(request.IsActive);

            context.AuditLogs.Add(new AuditLog(
                "integrations.ai-config.update",
                tenantId,
                request.ActorUserId,
                $"provider={request.Provider};model={request.DefaultModel}",
                DateTimeOffset.UtcNow));
        }

        if (request.IsActive)
        {
            var others = await context.WorkspaceAiConfigs
                .Where(x => x.TenantId == tenantId && x.Id != config.Id && x.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var other in others)
                other.SetActive(false);
        }

        await context.SaveChangesAsync(cancellationToken);

        await publishEndpoint.Publish(
            new AiConfigUpdatedEvent(
                config.TenantId,
                config.Provider.ToString(),
                config.EncryptedApiKey,
                config.BaseUrl,
                config.DefaultModel,
                null,
                config.Parameters,
                config.IsActive),
            cancellationToken);

        logger.LogInformation("Workspace AI configuration {ConfigId} saved for provider {Provider}", config.Id, request.Provider);

        return config.Id;
    }
}
