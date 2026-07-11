using FluentAssertions;
using MockQueryable.NSubstitute;
using NexConvo.Knowledge.Application.Common.Interfaces;
using NexConvo.Knowledge.Application.Features.KnowledgeBase.Queries.GetKnowledgeDocumentsPaged;
using NexConvo.Knowledge.Domain.Entities;
using NexConvo.Knowledge.Domain.Enums;
using NSubstitute;

namespace NexConvo.Knowledge.Application.UnitTests;

public class GetKnowledgeDocumentsPagedQueryHandlerTests
{
    private readonly IKnowledgeDbContext _dbMock = Substitute.For<IKnowledgeDbContext>();
    private readonly Guid _tenantId = Guid.NewGuid();

    private void SetupDb(List<KnowledgeDocument> documents)
    {
        var documentSet = documents.AsQueryable().BuildMockDbSet();
        _dbMock.KnowledgeDocuments.Returns(documentSet);
    }

    private static KnowledgeDocument MakeDocument(Guid tenantId, string fileName, DocumentStatus status)
    {
        var doc = new KnowledgeDocument(tenantId, fileName, $"hash-{Guid.NewGuid():N}");
        doc.SetStatus(status);
        return doc;
    }

    [Fact]
    public async Task Handle_ReturnsOnlyActiveDocuments_WithTotal()
    {
        var active1 = MakeDocument(_tenantId, "a.pdf", DocumentStatus.Ready);
        var active2 = MakeDocument(_tenantId, "b.pdf", DocumentStatus.Ready);
        var inactive = MakeDocument(_tenantId, "c.pdf", DocumentStatus.Ready);
        inactive.SetActive(false);
        SetupDb([active1, active2, inactive]);

        var handler = new GetKnowledgeDocumentsPagedQueryHandler(_dbMock);
        var result = await handler.Handle(new GetKnowledgeDocumentsPagedQuery(1, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Total.Should().Be(2);
        result.Value.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_CapsPageSize_AtServerMaximum()
    {
        var documents = Enumerable.Range(0, 150)
            .Select(i => MakeDocument(_tenantId, $"doc{i}.pdf", DocumentStatus.Ready))
            .ToList();
        SetupDb(documents);

        var handler = new GetKnowledgeDocumentsPagedQueryHandler(_dbMock);
        var result = await handler.Handle(new GetKnowledgeDocumentsPagedQuery(1, 1000), CancellationToken.None);

        result.Value!.Items.Should().HaveCount(100);
        result.Value.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task Handle_FiltersByStatus_WhenProvided()
    {
        var ready = MakeDocument(_tenantId, "a.pdf", DocumentStatus.Ready);
        var failed = MakeDocument(_tenantId, "b.pdf", DocumentStatus.Failed);
        SetupDb([ready, failed]);

        var handler = new GetKnowledgeDocumentsPagedQueryHandler(_dbMock);
        var result = await handler.Handle(
            new GetKnowledgeDocumentsPagedQuery(1, 20, Status: DocumentStatus.Failed), CancellationToken.None);

        result.Value!.Items.Should().ContainSingle(d => d.FileName == "b.pdf");
    }

    [Fact]
    public async Task Handle_FiltersBySourceType_WhenProvided()
    {
        var fileDoc = MakeDocument(_tenantId, "a.pdf", DocumentStatus.Ready);
        var urlDoc = KnowledgeDocument.ForUrl(_tenantId, "site", "https://example.com", "hash-url");
        SetupDb([fileDoc, urlDoc]);

        var handler = new GetKnowledgeDocumentsPagedQueryHandler(_dbMock);
        var result = await handler.Handle(
            new GetKnowledgeDocumentsPagedQuery(1, 20, SourceType: SourceType.Url), CancellationToken.None);

        result.Value!.Items.Should().ContainSingle(d => d.Title == "site");
    }
}
