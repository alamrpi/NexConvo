using FluentAssertions;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Knowledge.Application.Common.Interfaces;
using NexConvo.Knowledge.Application.Features.KnowledgeBase.Commands;
using NexConvo.Knowledge.Domain.Entities;
using NSubstitute;

namespace NexConvo.Knowledge.Application.UnitTests;

public class DeleteKnowledgeDocumentCommandHandlerTests
{
    private readonly IKnowledgeDbContext _dbMock = Substitute.For<IKnowledgeDbContext>();
    private readonly ITenantContext _tenantMock = Substitute.For<ITenantContext>();
    private readonly ILogger<DeleteKnowledgeDocumentCommandHandler> _loggerMock =
        Substitute.For<ILogger<DeleteKnowledgeDocumentCommandHandler>>();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly DeleteKnowledgeDocumentCommandHandler _handler;

    public DeleteKnowledgeDocumentCommandHandlerTests()
    {
        _tenantMock.TenantId.Returns(_tenantId);
        _handler = new DeleteKnowledgeDocumentCommandHandler(_dbMock, _tenantMock, _loggerMock);
    }

    private void SetupDb(List<KnowledgeDocument> documents, List<KnowledgeChunk> chunks)
    {
        var documentSet = documents.AsQueryable().BuildMockDbSet();
        var chunkSet = chunks.AsQueryable().BuildMockDbSet();
        var auditSet = new List<KnowledgeAuditLog>().AsQueryable().BuildMockDbSet();
        _dbMock.KnowledgeDocuments.Returns(documentSet);
        _dbMock.KnowledgeChunks.Returns(chunkSet);
        _dbMock.KnowledgeAuditLogs.Returns(auditSet);
    }

    [Fact]
    public async Task Delete_SoftDeletesDocumentAndChunks_AndWritesAudit()
    {
        var document = new KnowledgeDocument(_tenantId, "handbook.pdf", "hash");
        var chunk1 = new KnowledgeChunk(_tenantId, document.Id, "a", 0, 1, 1);
        var chunk2 = new KnowledgeChunk(_tenantId, document.Id, "b", 1, 1, 1);
        SetupDb([document], [chunk1, chunk2]);

        var result = await _handler.Handle(
            new DeleteKnowledgeDocumentCommand(document.Id, _actorId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        document.IsActive.Should().BeFalse();
        chunk1.IsActive.Should().BeFalse();
        chunk2.IsActive.Should().BeFalse();
        _dbMock.KnowledgeAuditLogs.Received(1).Add(Arg.Is<KnowledgeAuditLog>(a =>
            a.Action == "knowledge.delete" && a.TenantId == _tenantId && a.UserId == _actorId));
        await _dbMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_UnknownDocument_ReturnsNotFound()
    {
        SetupDb([], []);

        var result = await _handler.Handle(
            new DeleteKnowledgeDocumentCommand(Guid.NewGuid(), _actorId),
            CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
        await _dbMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
