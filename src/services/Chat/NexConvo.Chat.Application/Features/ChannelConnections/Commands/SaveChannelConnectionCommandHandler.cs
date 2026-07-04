using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Application.Security;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Chat.Application.Common;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Domain.Entities;
using NexConvo.Contracts.Events.Chat;

namespace NexConvo.Chat.Application.Features.ChannelConnections.Commands;

public sealed class SaveChannelConnectionCommandHandler(
    IChatDbContext db,
    ITenantContext tenant,
    IAesEncryptionService encryption,
    IPublishEndpoint publisher,
    ILogger<SaveChannelConnectionCommandHandler> logger)
    : IRequestHandler<SaveChannelConnectionCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(SaveChannelConnectionCommand cmd, CancellationToken ct)
    {
        var tenantId = tenant.TenantId;

        var existing = await db.ChannelConnections
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId
                  && x.Channel == cmd.Channel
                  && x.ExternalAccountId == cmd.ExternalAccountId,
                ct);

        var encryptedToken = encryption.Encrypt(cmd.AccessToken);
        var encryptedAppSecret = cmd.AppSecret is not null ? encryption.Encrypt(cmd.AppSecret) : null;

        if (existing is null)
        {
            existing = new ChannelConnection(
                tenantId,
                cmd.Channel,
                cmd.ExternalAccountId,
                cmd.AccountName,
                encryptedToken,
                encryptedAppSecret);

            existing.CreatedByUserId = cmd.ActorUserId;
            db.ChannelConnections.Add(existing);

            db.ChatAuditLogs.Add(new ChatAuditLog(
                "chat.channel-connection.create",
                tenantId,
                cmd.ActorUserId,
                $"channel={cmd.Channel};account={cmd.ExternalAccountId}",
                DateTimeOffset.UtcNow));
        }
        else
        {
            existing.UpdateToken(encryptedToken, cmd.AccountName, encryptedAppSecret);
            existing.SetActive(true);

            db.ChatAuditLogs.Add(new ChatAuditLog(
                "chat.channel-connection.update",
                tenantId,
                cmd.ActorUserId,
                $"channel={cmd.Channel};account={cmd.ExternalAccountId}",
                DateTimeOffset.UtcNow));
        }

        await db.SaveChangesAsync(ct);

        await publisher.Publish(
            new ChannelConnectionUpdatedEvent(
                tenantId,
                existing.Id,
                existing.Channel.ToLeadSourceChannel(),
                existing.ExternalAccountId,
                existing.IsActive),
            ct);

        logger.LogInformation(
            "Channel connection {ConnectionId} saved for channel {Channel}",
            existing.Id, cmd.Channel);

        return Result.Success(existing.Id);
    }
}
