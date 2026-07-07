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

namespace NexConvo.Integrations.Application.Features.AiConfig.Commands;

public sealed class SaveAiConfigCommandHandler(
    IIntegrationsDbContext context,
    ITenantContext tenant,
    IAesEncryptionService encryptionService,
    IPublishEndpoint publishEndpoint,
    IConnectionTester<AiTestInput> tester,
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
        var isNewConfig = config is null;

        if (isNewConfig)
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
        }
        else
        {
            var encryptedKey = hasNewKey ? encryptionService.Encrypt(request.ApiKey!) : config!.EncryptedApiKey;

            config!.UpdateSettings(
                encryptedKey,
                request.BaseUrl,
                request.DefaultModel,
                null,
                parameters);

            config.SetActive(request.IsActive);
        }

        // Re-test the API key server-side whenever it's new/changed, before anything is
        // persisted. A failing probe throws (mapped to 422) and nothing is saved. An unchanged
        // key skips the re-test and leaves prior health untouched (mirrors S3 Task 9).
        if (hasNewKey)
        {
            var probe = await tester.TestAsync(
                new AiTestInput(request.Provider, request.ApiKey!, request.BaseUrl, request.DefaultModel),
                cancellationToken);

            if (!probe.Success)
                throw new ConnectionTestFailedException("ai", probe.ErrorMessage);

            config.ApplyHealth(probe);
        }

        if (isNewConfig)
        {
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
