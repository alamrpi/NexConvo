using FluentAssertions;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.Knowledge.Application.Common.Interfaces;
using NexConvo.Knowledge.Application.Features.KnowledgeBase.Commands;
using NexConvo.Knowledge.Domain.Entities;
using NSubstitute;

namespace NexConvo.Knowledge.Application.UnitTests;

public class UploadKnowledgeDocumentCommandHandlerTests
{
    private readonly IKnowledgeDbContext _dbMock = Substitute.For<IKnowledgeDbContext>();
    private readonly ITenantContext _tenantMock = Substitute.For<ITenantContext>();
    private readonly IKnowledgeIngestionJobRunner _jobRunnerMock = Substitute.For<IKnowledgeIngestionJobRunner>();
    private readonly ILogger<UploadKnowledgeDocumentCommandHandler> _loggerMock =
        Substitute.For<ILogger<UploadKnowledgeDocumentCommandHandler>>();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly UploadKnowledgeDocumentCommandHandler _handler;

    public UploadKnowledgeDocumentCommandHandlerTests()
    {
        _tenantMock.TenantId.Returns(_tenantId);
        _handler = new UploadKnowledgeDocumentCommandHandler(_dbMock, _tenantMock, _jobRunnerMock, _loggerMock);
    }

    private void SetupDb(params KnowledgeDocument[] documents)
    {
        var documentSet = documents.ToList().AsQueryable().BuildMockDbSet();
        var auditSet = new List<KnowledgeAuditLog>().AsQueryable().BuildMockDbSet();
        _dbMock.KnowledgeDocuments.Returns(documentSet);
        _dbMock.KnowledgeAuditLogs.Returns(auditSet);
    }

    [Fact]
    public async Task Upload_CreatesDocument_WritesAudit_AndEnqueuesIngestion()
    {
        SetupDb();

        var result = await _handler.Handle(
            new UploadKnowledgeDocumentCommand("handbook.pdf", "hash-abc123", _actorId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _dbMock.KnowledgeDocuments.Received(1).Add(Arg.Is<KnowledgeDocument>(d =>
            d.TenantId == _tenantId && d.FileName == "handbook.pdf" && d.ContentHash == "hash-abc123"));
        _dbMock.KnowledgeAuditLogs.Received(1).Add(Arg.Is<KnowledgeAuditLog>(a =>
            a.Action == "knowledge.upload" && a.TenantId == _tenantId && a.UserId == _actorId));
        await _dbMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _jobRunnerMock.Received(1).EnqueueIngestion(result.Value, _tenantId);
    }

    [Fact]
    public async Task Upload_WithDuplicateContentHash_ReturnsExistingId_WithoutCreatingOrEnqueuing()
    {
        var existing = new KnowledgeDocument(_tenantId, "handbook.pdf", "hash-abc123");
        SetupDb(existing);

        var result = await _handler.Handle(
            new UploadKnowledgeDocumentCommand("renamed.pdf", "hash-abc123", _actorId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(existing.Id);
        _dbMock.KnowledgeDocuments.DidNotReceive().Add(Arg.Any<KnowledgeDocument>());
        await _dbMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        _jobRunnerMock.DidNotReceive().EnqueueIngestion(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Fact]
    public async Task Upload_WithInactiveDuplicate_CreatesFreshDocument()
    {
        var deleted = new KnowledgeDocument(_tenantId, "handbook.pdf", "hash-abc123");
        deleted.SetActive(false);
        SetupDb(deleted);

        var result = await _handler.Handle(
            new UploadKnowledgeDocumentCommand("handbook.pdf", "hash-abc123", _actorId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(deleted.Id);
        _dbMock.KnowledgeDocuments.Received(1).Add(Arg.Any<KnowledgeDocument>());
    }
}
