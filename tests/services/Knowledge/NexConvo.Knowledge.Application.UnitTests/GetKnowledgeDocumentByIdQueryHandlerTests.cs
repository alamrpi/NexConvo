using FluentAssertions;
using MockQueryable.NSubstitute;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Knowledge.Application.Common.Interfaces;
using NexConvo.Knowledge.Application.Features.KnowledgeBase.Queries.GetKnowledgeDocumentById;
using NexConvo.Knowledge.Domain.Entities;
using NSubstitute;

namespace NexConvo.Knowledge.Application.UnitTests;

public class GetKnowledgeDocumentByIdQueryHandlerTests
{
    private readonly IKnowledgeDbContext _dbMock = Substitute.For<IKnowledgeDbContext>();
    private readonly Guid _tenantId = Guid.NewGuid();

    private void SetupDb(List<KnowledgeDocument> documents, List<KnowledgeChunk> chunks)
    {
        var documentSet = documents.AsQueryable().BuildMockDbSet();
        var chunkSet = chunks.AsQueryable().BuildMockDbSet();
        _dbMock.KnowledgeDocuments.Returns(documentSet);
        _dbMock.KnowledgeChunks.Returns(chunkSet);
    }

    [Fact]
    public async Task Handle_ExistingActiveDocument_ReturnsDetailWithChunkPreview()
    {
        var document = new KnowledgeDocument(_tenantId, "handbook.pdf", "hash");
        document.SetReady(2, "BAAI/bge-m3", 1024);
        var chunk1 = new KnowledgeChunk(_tenantId, document.Id, "chunk one", 0, 3, document.Version);
        var chunk2 = new KnowledgeChunk(_tenantId, document.Id, "chunk two", 1, 3, document.Version);
        SetupDb([document], [chunk1, chunk2]);

        var handler = new GetKnowledgeDocumentByIdQueryHandler(_dbMock);
        var result = await handler.Handle(
            new GetKnowledgeDocumentByIdQuery(document.Id, ChunkPage: 1, ChunkPageSize: 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.FileName.Should().Be("handbook.pdf");
        result.Value.ChunkCount.Should().Be(2);
        result.Value.Chunks.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_UnknownDocument_ReturnsNotFound()
    {
        SetupDb([], []);

        var handler = new GetKnowledgeDocumentByIdQueryHandler(_dbMock);
        var result = await handler.Handle(
            new GetKnowledgeDocumentByIdQuery(Guid.NewGuid(), 1, 20), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Handle_SoftDeletedDocument_ReturnsNotFound()
    {
        var document = new KnowledgeDocument(_tenantId, "handbook.pdf", "hash");
        document.SetActive(false);
        SetupDb([document], []);

        var handler = new GetKnowledgeDocumentByIdQueryHandler(_dbMock);
        var result = await handler.Handle(
            new GetKnowledgeDocumentByIdQuery(document.Id, 1, 20), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Handle_CapsChunkPageSize_AtServerMaximum()
    {
        var document = new KnowledgeDocument(_tenantId, "handbook.pdf", "hash");
        var chunks = Enumerable.Range(0, 150)
            .Select(i => new KnowledgeChunk(_tenantId, document.Id, $"chunk {i}", i, 3, document.Version))
            .ToList();
        SetupDb([document], chunks);

        var handler = new GetKnowledgeDocumentByIdQueryHandler(_dbMock);
        var result = await handler.Handle(
            new GetKnowledgeDocumentByIdQuery(document.Id, ChunkPage: 1, ChunkPageSize: 1000), CancellationToken.None);

        result.Value!.Chunks.Should().HaveCount(100);
    }
}
