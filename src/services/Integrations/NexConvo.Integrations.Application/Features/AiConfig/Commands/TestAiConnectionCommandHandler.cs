using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Ai;
using NexConvo.BuildingBlocks.Ai.Services;
using NexConvo.BuildingBlocks.Application.Security;
using NexConvo.BuildingBlocks.Multitenancy;

namespace NexConvo.Integrations.Application.Features.AiConfig.Commands;

public sealed class TestAiConnectionCommandHandler(
    IIntegrationsDbContext context,
    ITenantContext tenant,
    IAiProviderFactory providerFactory,
    IAesEncryptionService encryptionService,
    ILogger<TestAiConnectionCommandHandler> logger)
    : IRequestHandler<TestAiConnectionCommand, TestAiConnectionResult>
{
    public async Task<TestAiConnectionResult> Handle(TestAiConnectionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            string apiKey;

            if (!string.IsNullOrWhiteSpace(request.ApiKey))
            {
                apiKey = request.ApiKey;
            }
            else
            {
                // No key supplied — use the stored (encrypted) key for this provider.
                var stored = await context.WorkspaceAiConfigs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.TenantId == tenant.TenantId && x.Provider == request.Provider,
                        cancellationToken);

                if (stored is null || string.IsNullOrEmpty(stored.EncryptedApiKey))
                    return new TestAiConnectionResult(false, "No API key configured for this provider.");

                apiKey = encryptionService.Decrypt(stored.EncryptedApiKey);
            }

            var provider = providerFactory.GetProvider(request.Provider);

            // Consume one chunk — enough to confirm auth + connectivity without burning tokens.
            await foreach (var _ in provider.GenerateStreamAsync(
                "Say OK",
                null,
                apiKey,
                request.Model,
                string.IsNullOrWhiteSpace(request.BaseUrl) ? null : request.BaseUrl,
                cancellationToken))
            {
                break;
            }

            return new TestAiConnectionResult(true, null);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("AI connection test failed for {Provider}: {Message}", request.Provider, ex.Message);
            return new TestAiConnectionResult(false, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogWarning("AI connection test failed for {Provider}: {Message}", request.Provider, ex.Message);
            return new TestAiConnectionResult(false, "Connection failed. Check the API key and model name.");
        }
    }
}
