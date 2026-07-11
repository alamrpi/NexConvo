using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.Knowledge.Application.Common.Interfaces;
using NexConvo.Knowledge.Application.Features.KnowledgeBase.Commands;
using NexConvo.Knowledge.Domain.Entities;
using NexConvo.Knowledge.Domain.Enums;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace NexConvo.Knowledge.Application.UnitTests;

public class UploadKnowledgeDocumentCommandHandlerTests
{
    private readonly IKnowledgeDbContext _dbMock = Substitute.For<IKnowledgeDbContext>();
    private readonly ITenantContext _tenantMock = Substitute.For<ITenantContext>();
    private readonly IKnowledgeIngestionJobRunner _jobRunnerMock = Substitute.For<IKnowledgeIngestionJobRunner>();
    private readonly IS3StorageService _s3Mock = Substitute.For<IS3StorageService>();
    private readonly IDnsResolver _dnsResolverMock = Substitute.For<IDnsResolver>();
    private readonly IUrlContentFetcher _urlFetcherMock = Substitute.For<IUrlContentFetcher>();
    private readonly ILogger<UploadKnowledgeDocumentCommandHandler> _loggerMock =
        Substitute.For<ILogger<UploadKnowledgeDocumentCommandHandler>>();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly UploadKnowledgeDocumentCommandHandler _handler;

    public UploadKnowledgeDocumentCommandHandlerTests()
    {
        _tenantMock.TenantId.Returns(_tenantId);
        _handler = new UploadKnowledgeDocumentCommandHandler(
            _dbMock, _tenantMock, _jobRunnerMock, _s3Mock, _dnsResolverMock, _urlFetcherMock, _loggerMock);
    }

    private void SetupDb(params KnowledgeDocument[] documents)
    {
        var documentSet = documents.ToList().AsQueryable().BuildMockDbSet();
        var auditSet = new List<KnowledgeAuditLog>().AsQueryable().BuildMockDbSet();
        _dbMock.KnowledgeDocuments.Returns(documentSet);
        _dbMock.KnowledgeAuditLogs.Returns(auditSet);
    }

    // ---- File ----

    [Fact]
    public async Task Upload_FileSource_HappyPath_PutsToS3_CreatesDocument_WritesAudit_AndEnqueuesIngestion()
    {
        SetupDb();
        using var fileStream = new MemoryStream([1, 2, 3]);

        var result = await _handler.Handle(
            new UploadKnowledgeDocumentCommand(
                SourceType.File, "handbook.pdf", "hash-abc123", _actorId,
                FileName: "handbook.pdf", FileStream: fileStream),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _s3Mock.Received(1).PutObjectAsync(
            _tenantId, Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _dbMock.KnowledgeDocuments.Received(1).Add(Arg.Is<KnowledgeDocument>(d =>
            d.TenantId == _tenantId && d.FileName == "handbook.pdf" && d.ContentHash == "hash-abc123"
            && d.S3ObjectKey != null));
        _dbMock.KnowledgeAuditLogs.Received(1).Add(Arg.Is<KnowledgeAuditLog>(a =>
            a.Action == "knowledge.upload" && a.TenantId == _tenantId && a.UserId == _actorId));
        await _dbMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _jobRunnerMock.Received(1).EnqueueIngestion(result.Value, _tenantId);
    }

    [Fact]
    public async Task Upload_FileSource_WithUnhealthyS3_ThrowsConnectionUnhealthy_CreatesNothing()
    {
        SetupDb();
        using var fileStream = new MemoryStream([1, 2, 3]);
        _s3Mock.PutObjectAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConnectionUnhealthyException("s3", "The workspace S3 connection is not healthy."));

        var act = () => _handler.Handle(
            new UploadKnowledgeDocumentCommand(
                SourceType.File, "handbook.pdf", "hash-abc123", _actorId,
                FileName: "handbook.pdf", FileStream: fileStream),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConnectionUnhealthyException>();
        _dbMock.KnowledgeDocuments.DidNotReceive().Add(Arg.Any<KnowledgeDocument>());
        await _dbMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        _jobRunnerMock.DidNotReceive().EnqueueIngestion(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Fact]
    public async Task Upload_WithDuplicateContentHash_ReturnsExistingId_WithoutCreatingOrEnqueuing()
    {
        var existing = new KnowledgeDocument(_tenantId, "handbook.pdf", "hash-abc123");
        SetupDb(existing);

        var result = await _handler.Handle(
            new UploadKnowledgeDocumentCommand(
                SourceType.File, "renamed.pdf", "hash-abc123", _actorId, FileName: "renamed.pdf"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(existing.Id);
        _dbMock.KnowledgeDocuments.DidNotReceive().Add(Arg.Any<KnowledgeDocument>());
        await _dbMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        _jobRunnerMock.DidNotReceive().EnqueueIngestion(Arg.Any<Guid>(), Arg.Any<Guid>());
        await _s3Mock.DidNotReceive().PutObjectAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_WithInactiveDuplicate_CreatesFreshDocument()
    {
        var deleted = new KnowledgeDocument(_tenantId, "handbook.pdf", "hash-abc123");
        deleted.SetActive(false);
        SetupDb(deleted);
        using var fileStream = new MemoryStream([1, 2, 3]);

        var result = await _handler.Handle(
            new UploadKnowledgeDocumentCommand(
                SourceType.File, "handbook.pdf", "hash-abc123", _actorId,
                FileName: "handbook.pdf", FileStream: fileStream),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(deleted.Id);
        _dbMock.KnowledgeDocuments.Received(1).Add(Arg.Any<KnowledgeDocument>());
    }

    // ---- Url ----

    [Fact]
    public async Task Upload_UrlSource_WithPrivateIpAddress_IsRejected_BeforeAnyHttpCall()
    {
        SetupDb();
        _dnsResolverMock.ResolveAsync("internal.example.com", Arg.Any<CancellationToken>())
            .Returns([IPAddress.Parse("10.0.0.5")]);

        var act = () => _handler.Handle(
            new UploadKnowledgeDocumentCommand(
                SourceType.Url, "Internal Doc", "hash-url", _actorId,
                SourceUrl: "https://internal.example.com/doc.html"),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotSupportedException>();
        await _urlFetcherMock.DidNotReceive().FetchAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>());
        _dbMock.KnowledgeDocuments.DidNotReceive().Add(Arg.Any<KnowledgeDocument>());
        await _dbMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        _jobRunnerMock.DidNotReceive().EnqueueIngestion(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Fact]
    public async Task Upload_UrlSource_WithLoopbackAddress_IsRejected_BeforeAnyHttpCall()
    {
        SetupDb();
        _dnsResolverMock.ResolveAsync("localhost.evil.example.com", Arg.Any<CancellationToken>())
            .Returns([IPAddress.Loopback]);

        var act = () => _handler.Handle(
            new UploadKnowledgeDocumentCommand(
                SourceType.Url, "Doc", "hash-url2", _actorId,
                SourceUrl: "https://localhost.evil.example.com/doc.html"),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotSupportedException>();
        await _urlFetcherMock.DidNotReceive().FetchAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_UrlSource_HappyPath_FetchesAndStoresToS3_CreatesDocument()
    {
        SetupDb();
        _dnsResolverMock.ResolveAsync("example.com", Arg.Any<CancellationToken>())
            .Returns([IPAddress.Parse("93.184.216.34")]);
        _urlFetcherMock.FetchAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .Returns(new FetchedUrlContent([1, 2, 3, 4], "text/html"));

        var result = await _handler.Handle(
            new UploadKnowledgeDocumentCommand(
                SourceType.Url, "Example Doc", "hash-url3", _actorId,
                SourceUrl: "https://example.com/doc.html"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _urlFetcherMock.Received(1).FetchAsync(
            Arg.Is<Uri>(u => u.Host == "example.com"), Arg.Any<CancellationToken>());
        await _s3Mock.Received(1).PutObjectAsync(
            _tenantId, Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _dbMock.KnowledgeDocuments.Received(1).Add(Arg.Is<KnowledgeDocument>(d =>
            d.SourceType == SourceType.Url && d.SourceUrl == "https://example.com/doc.html"));
        _jobRunnerMock.Received(1).EnqueueIngestion(result.Value, _tenantId);
    }

    // ---- Text ----

    [Fact]
    public async Task Upload_TextSource_HappyPath_StoresRawTextToS3_CreatesDocument()
    {
        SetupDb();

        var result = await _handler.Handle(
            new UploadKnowledgeDocumentCommand(
                SourceType.Text, "Notes", "hash-text", _actorId,
                RawText: "Some knowledge base content."),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _s3Mock.Received(1).PutObjectAsync(
            _tenantId, Arg.Any<string>(), Arg.Any<Stream>(), "text/plain", Arg.Any<CancellationToken>());
        _dbMock.KnowledgeDocuments.Received(1).Add(Arg.Is<KnowledgeDocument>(d =>
            d.SourceType == SourceType.Text && d.Title == "Notes"));
        _jobRunnerMock.Received(1).EnqueueIngestion(result.Value, _tenantId);
    }

    // ---- Faq ----

    [Fact]
    public async Task Upload_FaqSource_HappyPath_SerializesPairs_StoresToS3_CreatesDocument()
    {
        SetupDb();
        var pairs = new List<FaqPair> { new("What are your hours?", "9am-5pm.") };

        var result = await _handler.Handle(
            new UploadKnowledgeDocumentCommand(
                SourceType.Faq, "FAQ", "hash-faq", _actorId,
                FaqPairs: pairs),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _s3Mock.Received(1).PutObjectAsync(
            _tenantId, Arg.Any<string>(), Arg.Any<Stream>(), "text/plain", Arg.Any<CancellationToken>());
        _dbMock.KnowledgeDocuments.Received(1).Add(Arg.Is<KnowledgeDocument>(d =>
            d.SourceType == SourceType.Faq && d.Title == "FAQ"));
        _jobRunnerMock.Received(1).EnqueueIngestion(result.Value, _tenantId);
    }
}
