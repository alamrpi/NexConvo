using FluentAssertions;
using NexConvo.Knowledge.Domain.Entities;

namespace NexConvo.Knowledge.Domain.Tests;

public class KnowledgeChunkTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid DocumentId = Guid.NewGuid();

    [Fact]
    public void Ctor_SetsAllProperties_AndIsActive()
    {
        var chunk = new KnowledgeChunk(
            TenantId,
            DocumentId,
            content: "Bengali-first chunk text",
            ordinal: 3,
            tokenCount: 42,
            documentVersion: 2);

        chunk.TenantId.Should().Be(TenantId);
        chunk.DocumentId.Should().Be(DocumentId);
        chunk.Content.Should().Be("Bengali-first chunk text");
        chunk.Ordinal.Should().Be(3);
        chunk.TokenCount.Should().Be(42);
        chunk.DocumentVersion.Should().Be(2);
        chunk.IsActive.Should().BeTrue();
    }

    [Fact]
    public void SetActive_False_SoftDeletes()
    {
        var chunk = new KnowledgeChunk(TenantId, DocumentId, "text", 0, 1, 1);

        chunk.SetActive(false);

        chunk.IsActive.Should().BeFalse();
    }
}
