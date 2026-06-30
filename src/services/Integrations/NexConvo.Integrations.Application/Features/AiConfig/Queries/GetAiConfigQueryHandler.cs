using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Multitenancy;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NexConvo.Integrations.Application.Features.AiConfig.Queries;

public sealed class GetAiConfigQueryHandler(
    IIntegrationsDbContext db,
    ITenantContext tenant,
    ILogger<GetAiConfigQueryHandler> logger)
    : IRequestHandler<GetAiConfigQuery, List<AiConfigDto>>
{
    public async Task<List<AiConfigDto>> Handle(GetAiConfigQuery request, CancellationToken ct)
    {
        // Tenant from the JWT (RLS also scopes this) — never from client input (Standard 6).
        var configs = await db.WorkspaceAiConfigs
            .Where(x => x.TenantId == tenant.TenantId)
            .AsNoTracking()
            .ToListAsync(ct);

        logger.LogInformation("Retrieved {Count} AI configs", configs.Count);

        // Never expose the key — only whether one is set (Standard 13/15).
        return configs.Select(c => new AiConfigDto(
            c.Id,
            c.Provider,
            !string.IsNullOrEmpty(c.EncryptedApiKey),
            c.BaseUrl,
            c.DefaultModel,
            c.Parameters,
            c.IsActive
        )).ToList();
    }
}
