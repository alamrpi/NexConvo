using FluentAssertions;
using NexConvo.Knowledge.Domain.Entities;

namespace NexConvo.Knowledge.Domain.Tests;

public class KnowledgeAuditLogTests
{
    [Fact]
    public void Ctor_SetsAllFields_AndGeneratesId()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;

        var entry = new KnowledgeAuditLog("knowledge.upload", tenantId, userId, "fileName=a.pdf", occurredAt);

        entry.Id.Should().NotBeEmpty();
        entry.Action.Should().Be("knowledge.upload");
        entry.TenantId.Should().Be(tenantId);
        entry.UserId.Should().Be(userId);
        entry.Detail.Should().Be("fileName=a.pdf");
        entry.OccurredAt.Should().Be(occurredAt);
    }
}
