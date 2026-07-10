using Microsoft.EntityFrameworkCore;
using NexConvo.Chat.Domain.Entities;

namespace NexConvo.Chat.Application.Common.Interfaces;

public interface IChatDbContext
{
    DbSet<ChannelConnection> ChannelConnections { get; }
    DbSet<WorkspaceChatSettings> WorkspaceChatSettings { get; }
    DbSet<ChatAuditLog> ChatAuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
