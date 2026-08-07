using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Application.Health;
using NexConvo.BuildingBlocks.Application.Security;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.Chat.Application.Common;
using NexConvo.Chat.Application.Common.Interfaces;

namespace NexConvo.Chat.Application.Features.ChannelConnections.Commands;

public sealed class TestChannelConnectionByIdCommandHandler(
    IChatDbContext db,
    ITenantContext tenant,
    IAesEncryptionService aes,
    IConnectionTester<ChannelTestInput> tester)
    : IRequestHandler<TestChannelConnectionByIdCommand, ConnectionHealth>
{
    public async Task<ConnectionHealth> Handle(TestChannelConnectionByIdCommand cmd, CancellationToken ct)
    {
        var tenantId = tenant.TenantId;

        var connection = await db.ChannelConnections
            .FirstOrDefaultAsync(x => x.Id == cmd.ConnectionId && x.TenantId == tenantId, ct);

        if (connection is null)
            return ConnectionHealth.Failed("Connection not found.", null);

        var decryptedToken = aes.Decrypt(connection.EncryptedAccessToken);

        var probe = await tester.TestAsync(
            new ChannelTestInput(connection.Channel, decryptedToken, connection.ExternalAccountId), ct);

        connection.ApplyHealth(probe);
        await db.SaveChangesAsync(ct);

        return probe;
    }
}
