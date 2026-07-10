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
}
