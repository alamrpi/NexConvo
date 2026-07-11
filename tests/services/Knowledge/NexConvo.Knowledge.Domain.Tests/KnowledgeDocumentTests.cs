using FluentAssertions;
using NexConvo.Knowledge.Domain.Entities;
using NexConvo.Knowledge.Domain.Enums;

namespace NexConvo.Knowledge.Domain.Tests;

public class KnowledgeDocumentTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public void Ctor_SetsDefaults_PendingVersionOneActive()
    {
        var document = new KnowledgeDocument(TenantId, "handbook.pdf", "abc123");

        document.TenantId.Should().Be(TenantId);
        document.FileName.Should().Be("handbook.pdf");
        document.ContentHash.Should().Be("abc123");
        document.Status.Should().Be(DocumentStatus.Pending);
        document.Version.Should().Be(1);
        document.IsActive.Should().BeTrue();
    }

    [Fact]
    public void SetStatus_TransitionsStatus()
    {
        var document = new KnowledgeDocument(TenantId, "handbook.pdf", "abc123");

        document.SetStatus(DocumentStatus.Processing);

        document.Status.Should().Be(DocumentStatus.Processing);
    }

    [Fact]
    public void SetActive_False_SoftDeletes()
    {
        var document = new KnowledgeDocument(TenantId, "handbook.pdf", "abc123");

        document.SetActive(false);

        document.IsActive.Should().BeFalse();
    }

    [Fact]
    public void IncrementVersionAndResetToPending_BumpsVersion_And_ResetsStatus()
    {
        var document = new KnowledgeDocument(TenantId, "handbook.pdf", "abc123");
        document.SetStatus(DocumentStatus.Ready);

        document.IncrementVersionAndResetToPending();

        document.Version.Should().Be(2);
        document.Status.Should().Be(DocumentStatus.Pending);
    }

    [Fact]
    public void SetFailed_SetsStatusFailed_And_FailureReason()
    {
        var document = new KnowledgeDocument(TenantId, "handbook.pdf", "abc123");
        document.SetStatus(DocumentStatus.Processing);

        document.SetFailed("PDF extraction threw an exception");

        document.Status.Should().Be(DocumentStatus.Failed);
        document.FailureReason.Should().Be("PDF extraction threw an exception");
    }

    [Fact]
    public void SetFailed_ClearsAnyPreviousFailureReason_WhenCalledAgain()
    {
        var document = new KnowledgeDocument(TenantId, "handbook.pdf", "abc123");
        document.SetFailed("first failure");

        document.SetFailed("second failure");

        document.FailureReason.Should().Be("second failure");
    }

    [Fact]
    public void SetReady_SetsStatusReady_ChunkCount_EmbeddingModel_And_Dimensions()
    {
        var document = new KnowledgeDocument(TenantId, "handbook.pdf", "abc123");
        document.SetStatus(DocumentStatus.Processing);

        document.SetReady(42, "BAAI/bge-m3", 1024);

        document.Status.Should().Be(DocumentStatus.Ready);
        document.ChunkCount.Should().Be(42);
        document.EmbeddingModel.Should().Be("BAAI/bge-m3");
        document.EmbeddingDimensions.Should().Be(1024);
    }

    [Fact]
    public void SetReady_ClearsAnyPreviousFailureReason()
    {
        var document = new KnowledgeDocument(TenantId, "handbook.pdf", "abc123");
        document.SetFailed("boom");

        document.SetReady(3, "BAAI/bge-m3", 1024);

        document.Status.Should().Be(DocumentStatus.Ready);
        document.FailureReason.Should().BeNull();
    }

    [Fact]
    public void SetS3ObjectKey_SetsTheKey()
    {
        var document = new KnowledgeDocument(TenantId, "handbook.pdf", "abc123");

        document.SetS3ObjectKey("tenants/abc/handbook.pdf");

        document.S3ObjectKey.Should().Be("tenants/abc/handbook.pdf");
    }

    [Fact]
    public void ForUrl_CreatesDocument_WithSourceTypeUrl_AndSourceUrlSet()
    {
        var document = KnowledgeDocument.ForUrl(TenantId, "Handbook", "https://example.com/h.html", "hash-url");

        document.TenantId.Should().Be(TenantId);
        document.SourceType.Should().Be(SourceType.Url);
        document.SourceUrl.Should().Be("https://example.com/h.html");
        document.Title.Should().Be("Handbook");
        document.ContentHash.Should().Be("hash-url");
        document.Status.Should().Be(DocumentStatus.Pending);
    }

    [Fact]
    public void ForText_CreatesDocument_WithSourceTypeText()
    {
        var document = KnowledgeDocument.ForText(TenantId, "Notes", "hash-text");

        document.SourceType.Should().Be(SourceType.Text);
        document.Title.Should().Be("Notes");
    }

    [Fact]
    public void ForFaq_CreatesDocument_WithSourceTypeFaq()
    {
        var document = KnowledgeDocument.ForFaq(TenantId, "FAQ", "hash-faq");

        document.SourceType.Should().Be(SourceType.Faq);
        document.Title.Should().Be("FAQ");
    }
}
