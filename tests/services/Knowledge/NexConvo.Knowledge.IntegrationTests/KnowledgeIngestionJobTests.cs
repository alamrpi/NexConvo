using FluentAssertions;
using Hangfire;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NexConvo.Contracts.Events.Knowledge;
using NexConvo.Knowledge.Domain.Entities;
using NexConvo.Knowledge.Domain.Enums;
using NexConvo.Knowledge.Infrastructure.Jobs;
using NSubstitute;

namespace NexConvo.Knowledge.IntegrationTests;

/// <summary>
/// Runs the stub ingestion job against the real RLS-enforced database. This is the regression
/// guard for the tenant-scoping fix: the job's tenant-pinned context must see (and transition)
/// its tenant's document even though there is no HTTP request scope.
/// </summary>
[Collection("knowledge-postgres")]
public class KnowledgeIngestionJobTests(KnowledgePostgresFixture fixture)
{
    [Fact]
    public async Task ExecuteAsync_TransitionsDocument_PendingToReady_AndPublishesStatusEvents()
    {
        var tenantId = Guid.NewGuid();
        Guid documentId;
        await using (var db = fixture.CreateTenantContext(tenantId))
        {
            var document = new KnowledgeDocument(tenantId, "handbook.pdf", $"hash-{Guid.NewGuid():N}");
            db.KnowledgeDocuments.Add(document);
            await db.SaveChangesAsync();
            documentId = document.Id;
        }

        var publishEndpoint = Substitute.For<IPublishEndpoint>();
        var job = new KnowledgeIngestionJob(
            fixture.CreateTenantContext,
            publishEndpoint,
            Substitute.For<IBackgroundJobClient>(),
            NullLogger<KnowledgeIngestionJob>.Instance);

        await job.ExecuteAsync(documentId, tenantId, CancellationToken.None);

        await using (var db = fixture.CreateTenantContext(tenantId))
        {
            var document = await db.KnowledgeDocuments.SingleAsync(d => d.Id == documentId);
            document.Status.Should().Be(DocumentStatus.Ready);
        }

        await publishEndpoint.Received(1).Publish(
            Arg.Is<KnowledgeDocumentIngestionStatusChangedEvent>(e =>
                e.DocumentId == documentId && e.NewStatus == (int)DocumentStatus.Processing),
            Arg.Any<CancellationToken>());
        await publishEndpoint.Received(1).Publish(
            Arg.Is<KnowledgeDocumentIngestionStatusChangedEvent>(e =>
                e.DocumentId == documentId && e.NewStatus == (int)DocumentStatus.Ready),
            Arg.Any<CancellationToken>());
    }
}
