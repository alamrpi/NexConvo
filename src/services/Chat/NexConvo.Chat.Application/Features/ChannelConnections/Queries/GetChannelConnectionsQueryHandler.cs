using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Application.Features.ChannelConnections.Dtos;

namespace NexConvo.Chat.Application.Features.ChannelConnections.Queries;

public sealed class GetChannelConnectionsQueryHandler(
    IChatDbContext db,
    ITenantContext tenant,
    ILogger<GetChannelConnectionsQueryHandler> logger)
    : IRequestHandler<GetChannelConnectionsQuery, List<ChannelConnectionDto>>
{
    public async Task<List<ChannelConnectionDto>> Handle(
        GetChannelConnectionsQuery request,
        CancellationToken ct)
    {
        var tenantId = tenant.TenantId;

        var connections = await db.ChannelConnections
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .AsNoTracking()
            .ToListAsync(ct);

        logger.LogInformation(
            "Retrieved {Count} channel connections for tenant {TenantId}",
            connections.Count, tenantId);

        return connections.Select(ChannelConnectionDto.FromEntity).ToList();
    }
}
