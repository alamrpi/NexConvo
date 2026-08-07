using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Chat.Application.Common;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Domain.Entities;
using NexConvo.Contracts.Events.Chat;

namespace NexConvo.Chat.Application.Features.ChannelConnections.Commands;

public sealed class DeleteChannelConnectionCommandHandler(
    IChatDbContext db,
    ITenantContext tenant,
    IPublishEndpoint publisher,
    ILogger<DeleteChannelConnectionCommandHandler> logger)
    : IRequestHandler<DeleteChannelConnectionCommand, Result>
{
    public async Task<Result> Handle(DeleteChannelConnectionCommand cmd, CancellationToken ct)
    {
        var tenantId = tenant.TenantId;

        var connection = await db.ChannelConnections
            .FirstOrDefaultAsync(
                x => x.Id == cmd.ConnectionId && x.TenantId == tenantId,
                ct);

        if (connection is null)
            return Result.NotFound($"Channel connection {cmd.ConnectionId} not found.");

        connection.SetActive(false);

        db.ChatAuditLogs.Add(new ChatAuditLog(
            "chat.channel-connection.delete",
            tenantId,
            cmd.ActorUserId,
            $"connectionId={cmd.ConnectionId}",
            DateTimeOffset.UtcNow));

        await db.SaveChangesAsync(ct);

        await publisher.Publish(
            new ChannelConnectionUpdatedEvent(
                tenantId,
                connection.Id,
                connection.Channel.ToLeadSourceChannel(),
                connection.ExternalAccountId,
                IsActive: false),
            ct);

        logger.LogInformation(
            "Channel connection {ConnectionId} soft-deleted", cmd.ConnectionId);

        return Result.Success();
    }
}
