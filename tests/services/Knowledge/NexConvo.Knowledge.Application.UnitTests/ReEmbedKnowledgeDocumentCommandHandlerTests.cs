using FluentAssertions;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Knowledge.Application.Common.Interfaces;
using NexConvo.Knowledge.Application.Features.KnowledgeBase.Commands;
using NexConvo.Knowledge.Domain.Entities;
using NexConvo.Knowledge.Domain.Enums;
using NSubstitute;

namespace NexConvo.Knowledge.Application.UnitTests;

public class ReEmbedKnowledgeDocumentCommandHandlerTests
{
    private readonly IKnowledgeDbContext _dbMock = Substitute.For<IKnowledgeDbContext>();
    private readonly ITenantContext _tenantMock = Substitute.For<ITenantContext>();
    private readonly IKnowledgeIngestionJobRunner _jobRunnerMock = Substitute.For<IKnowledgeIngestionJobRunner>();
    private readonly ILogger<ReEmbedKnowledgeDocumentCommandHandler> _loggerMock =
        Substitute.For<ILogger<ReEmbedKnowledgeDocumentCommandHandler>>();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly ReEmbedKnowledgeDocumentCommandHandler _handler;

    public ReEmbedKnowledgeDocumentCommandHandlerTests()
    {
        _tenantMock.TenantId.Returns(_tenantId);
        _handler = new ReEmbedKnowledgeDocumentCommandHandler(_dbMock, _tenantMock, _jobRunnerMock, _loggerMock);
    }

    private void SetupDb(params KnowledgeDocument[] documents)
    {
        var documentSet = documents.ToList().AsQueryable().BuildMockDbSet();
        var auditSet = new List<KnowledgeAuditLog>().AsQueryable().BuildMockDbSet();
        _dbMock.KnowledgeDocuments.Returns(documentSet);
        _dbMock.KnowledgeAuditLogs.Returns(auditSet);
    }

    [Fact]
    public async Task ReEmbed_IncrementsVersion_ResetsToPending_Audits_AndEnqueues()
    {
        var document = new KnowledgeDocument(_tenantId, "handbook.pdf", "hash");
        document.SetStatus(DocumentStatus.Ready);
        SetupDb(document);

        var result = await _handler.Handle(
            new ReEmbedKnowledgeDocumentCommand(document.Id, _actorId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        document.Version.Should().Be(2);
        document.Status.Should().Be(DocumentStatus.Pending);
        _dbMock.KnowledgeAuditLogs.Received(1).Add(Arg.Is<KnowledgeAuditLog>(a =>
            a.Action == "knowledge.re-embed" && a.TenantId == _tenantId));
        await _dbMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _jobRunnerMock.Received(1).EnqueueIngestion(document.Id, _tenantId);
    }

    [Fact]
    public async Task ReEmbed_UnknownDocument_ReturnsNotFound_WithoutEnqueuing()
    {
        SetupDb();

        var result = await _handler.Handle(
            new ReEmbedKnowledgeDocumentCommand(Guid.NewGuid(), _actorId),
            CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
        _jobRunnerMock.DidNotReceive().EnqueueIngestion(Arg.Any<Guid>(), Arg.Any<Guid>());
    }
}
