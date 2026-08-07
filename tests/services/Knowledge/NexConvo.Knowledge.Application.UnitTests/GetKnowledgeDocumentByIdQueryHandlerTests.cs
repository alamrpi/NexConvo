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

    [Fact]
    public async Task Handle_AlwaysPopulatesVersionHistory_IncludingSupersededVersions()
    {
        // Regression guard: the frontend detail page renders doc.versionHistory.map(...) directly
        // and crashes (TypeError: Cannot read properties of undefined) if this field is ever
        // missing — it was previously omitted entirely from the DTO. A re-embedded document's
        // prior-version chunks are soft-deactivated (D4-5), not deleted, so both versions' history
        // must be reconstructible from knowledge_chunks alone.
        var document = new KnowledgeDocument(_tenantId, "handbook.pdf", "hash");
        document.SetReady(1, "BAAI/bge-m3", 1024);
        var v1Chunk = new KnowledgeChunk(_tenantId, document.Id, "v1 chunk", 0, 3, documentVersion: 1);
        v1Chunk.SetActive(false);
        var v2Chunk = new KnowledgeChunk(_tenantId, document.Id, "v2 chunk", 0, 3, documentVersion: 2);
        SetupDb([document], [v1Chunk, v2Chunk]);

        var handler = new GetKnowledgeDocumentByIdQueryHandler(_dbMock);
        var result = await handler.Handle(
            new GetKnowledgeDocumentByIdQuery(document.Id, ChunkPage: 1, ChunkPageSize: 20), CancellationToken.None);

        result.Value!.VersionHistory.Should().HaveCount(2);
        result.Value.VersionHistory.Should().Contain(v => v.Version == 1 && v.ChunkCount == 1);
        result.Value.VersionHistory.Should().Contain(v => v.Version == 2 && v.ChunkCount == 1);
    }
}
