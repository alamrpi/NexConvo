using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Application.Health;
using NexConvo.BuildingBlocks.Application.Security;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Chat.Application.Common;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Application.Features.ChannelConnections.Dtos;
using NexConvo.Chat.Domain.Entities;
using NexConvo.Contracts.Events.Chat;

namespace NexConvo.Chat.Application.Features.ChannelConnections.Commands;

public sealed class SaveChannelConnectionCommandHandler(
    IChatDbContext db,
    ITenantContext tenant,
    IAesEncryptionService encryption,
    IPublishEndpoint publisher,
    IConnectionTester<ChannelTestInput> tester,
    ILogger<SaveChannelConnectionCommandHandler> logger)
    : IRequestHandler<SaveChannelConnectionCommand, Result<ChannelConnectionDto>>
{
    public async Task<Result<ChannelConnectionDto>> Handle(SaveChannelConnectionCommand cmd, CancellationToken ct)
    {
        var tenantId = tenant.TenantId;

        var existing = await db.ChannelConnections
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId
                  && x.Channel == cmd.Channel
                  && x.ExternalAccountId == cmd.ExternalAccountId,
                ct);

        var hasNewToken = !string.IsNullOrWhiteSpace(cmd.AccessToken);
        var encryptedToken = hasNewToken ? encryption.Encrypt(cmd.AccessToken) : existing?.EncryptedAccessToken ?? string.Empty;
        var encryptedAppSecret = cmd.AppSecret is not null ? encryption.Encrypt(cmd.AppSecret) : null;

        ChannelConnection connection;
        bool isNewConnection = existing is null;

        if (isNewConnection)
        {
            connection = new ChannelConnection(
                tenantId,
                cmd.Channel,
                cmd.ExternalAccountId,
                cmd.AccountName,
                encryptedToken,
                encryptedAppSecret)
            {
                CreatedByUserId = cmd.ActorUserId,
            };
        }
        else
        {
            connection = existing!;
            connection.UpdateToken(encryptedToken, cmd.AccountName, encryptedAppSecret);
            connection.SetActive(true);
        }

        // Re-test the access token server-side whenever it's new/changed, before anything is
        // persisted. A failing probe throws (mapped to 422) and nothing is added/audited/saved/
        // published. An unchanged token skips the re-test and leaves prior health untouched
        // (mirrors SaveAiConfigCommandHandler). Web has no token, so hasNewToken is false and the
        // gate is naturally skipped for it.
        if (hasNewToken)
        {
            var probe = await tester.TestAsync(
                new ChannelTestInput(cmd.Channel, cmd.AccessToken, cmd.ExternalAccountId), ct);

            if (!probe.Success)
                throw new ConnectionTestFailedException("channel", probe.ErrorMessage);

            connection.ApplyHealth(probe);
        }

        if (isNewConnection)
        {
            db.ChannelConnections.Add(connection);

            db.ChatAuditLogs.Add(new ChatAuditLog(
                "chat.channel-connection.create",
                tenantId,
                cmd.ActorUserId,
                $"channel={cmd.Channel};account={cmd.ExternalAccountId}",
                DateTimeOffset.UtcNow));
        }
        else
        {
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
                connection.Id,
                connection.Channel.ToLeadSourceChannel(),
                connection.ExternalAccountId,
                connection.IsActive),
            ct);

        logger.LogInformation(
            "Channel connection {ConnectionId} saved for channel {Channel}",
            connection.Id, cmd.Channel);

        return Result.Success(ChannelConnectionDto.FromEntity(connection));
    }
}
