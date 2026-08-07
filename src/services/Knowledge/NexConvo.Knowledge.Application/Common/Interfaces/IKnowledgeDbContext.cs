using Microsoft.EntityFrameworkCore;
using NexConvo.Knowledge.Domain.Entities;

namespace NexConvo.Knowledge.Application.Common.Interfaces;

public interface IKnowledgeDbContext
{
    DbSet<KnowledgeDocument> KnowledgeDocuments { get; }
    DbSet<KnowledgeChunk> KnowledgeChunks { get; }
    DbSet<KnowledgeAuditLog> KnowledgeAuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
