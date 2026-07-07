using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Application.Health;
using NexConvo.BuildingBlocks.Application.Security;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.BuildingBlocks.Multitenancy;

namespace NexConvo.Integrations.Application.Features.AiConfig.Commands;

public sealed class TestAiConnectionCommandHandler(
    IIntegrationsDbContext context,
    ITenantContext tenant,
    IAesEncryptionService encryptionService,
    IConnectionTester<AiTestInput> tester)
    : IRequestHandler<TestAiConnectionCommand, ConnectionHealth>
{
    public async Task<ConnectionHealth> Handle(TestAiConnectionCommand request, CancellationToken cancellationToken)
    {
        string apiKey = request.ApiKey ?? "";
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            var stored = await context.WorkspaceAiConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.TenantId == tenant.TenantId && x.Provider == request.Provider,
                    cancellationToken);

            if (stored is null || string.IsNullOrEmpty(stored.EncryptedApiKey))
                return ConnectionHealth.Failed("No API key configured for this provider.", null);

            apiKey = encryptionService.Decrypt(stored.EncryptedApiKey);
        }

        return await tester.TestAsync(
            new AiTestInput(request.Provider, apiKey, request.BaseUrl, request.Model), cancellationToken);
    }
}
