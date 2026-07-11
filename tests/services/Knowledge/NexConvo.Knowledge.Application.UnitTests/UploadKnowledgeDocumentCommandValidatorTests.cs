using FluentAssertions;
using NexConvo.Knowledge.Application.Features.KnowledgeBase.Commands;
using NexConvo.Knowledge.Domain.Enums;

namespace NexConvo.Knowledge.Application.UnitTests;

public class UploadKnowledgeDocumentCommandValidatorTests
{
    private readonly UploadKnowledgeDocumentCommandValidator _validator = new();

    private static UploadKnowledgeDocumentCommand BaseCommand(SourceType sourceType) => new(
        SourceType: sourceType,
        Title: "Handbook",
        ContentHash: "hash-abc123",
        ActorUserId: Guid.NewGuid());

    // ---- File ----

    [Fact]
    public void File_WithFileNameAndStream_IsValid()
    {
        var cmd = BaseCommand(SourceType.File) with
        {
            FileName = "handbook.pdf",
            FileStream = new MemoryStream([1, 2, 3]),
        };

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void File_WithoutFileName_IsInvalid()
    {
        var cmd = BaseCommand(SourceType.File) with
        {
            FileName = null,
            FileStream = new MemoryStream([1, 2, 3]),
        };

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UploadKnowledgeDocumentCommand.FileName));
    }

    [Fact]
    public void File_WithoutFileStream_IsInvalid()
    {
        var cmd = BaseCommand(SourceType.File) with
        {
            FileName = "handbook.pdf",
            FileStream = null,
        };

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UploadKnowledgeDocumentCommand.FileStream));
    }

    // ---- Url ----

    [Fact]
    public void Url_WithWellFormedHttpsUrl_IsValid()
    {
        var cmd = BaseCommand(SourceType.Url) with { SourceUrl = "https://example.com/docs/handbook" };

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Url_Missing_IsInvalid()
    {
        var cmd = BaseCommand(SourceType.Url) with { SourceUrl = null };

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UploadKnowledgeDocumentCommand.SourceUrl));
    }

    [Fact]
    public void Url_WithHttpScheme_IsInvalid()
    {
        var cmd = BaseCommand(SourceType.Url) with { SourceUrl = "http://example.com/docs" };

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UploadKnowledgeDocumentCommand.SourceUrl));
    }

    [Fact]
    public void Url_WithRelativeUrl_IsInvalid()
    {
        var cmd = BaseCommand(SourceType.Url) with { SourceUrl = "/docs/handbook" };

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UploadKnowledgeDocumentCommand.SourceUrl));
    }

    [Fact]
    public void Url_WithMalformedUrl_IsInvalid()
    {
        var cmd = BaseCommand(SourceType.Url) with { SourceUrl = "not a url" };

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UploadKnowledgeDocumentCommand.SourceUrl));
    }

    // ---- Text ----

    [Fact]
    public void Text_WithNonEmptyRawText_IsValid()
    {
        var cmd = BaseCommand(SourceType.Text) with { RawText = "Some knowledge base content." };

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Text_Missing_IsInvalid()
    {
        var cmd = BaseCommand(SourceType.Text) with { RawText = null };

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UploadKnowledgeDocumentCommand.RawText));
    }

    [Fact]
    public void Text_Empty_IsInvalid()
    {
        var cmd = BaseCommand(SourceType.Text) with { RawText = "   " };

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UploadKnowledgeDocumentCommand.RawText));
    }

    // ---- Faq ----

    [Fact]
    public void Faq_WithNonEmptyPairs_IsValid()
    {
        var cmd = BaseCommand(SourceType.Faq) with
        {
            FaqPairs = [new FaqPair("What are your hours?", "9am-5pm.")],
        };

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Faq_Missing_IsInvalid()
    {
        var cmd = BaseCommand(SourceType.Faq) with { FaqPairs = null };

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UploadKnowledgeDocumentCommand.FaqPairs));
    }

    [Fact]
    public void Faq_WithEmptyList_IsInvalid()
    {
        var cmd = BaseCommand(SourceType.Faq) with { FaqPairs = [] };

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UploadKnowledgeDocumentCommand.FaqPairs));
    }

    [Fact]
    public void Faq_WithPairMissingQuestion_IsInvalid()
    {
        var cmd = BaseCommand(SourceType.Faq) with
        {
            FaqPairs = [new FaqPair("", "9am-5pm.")],
        };

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Faq_WithPairMissingAnswer_IsInvalid()
    {
        var cmd = BaseCommand(SourceType.Faq) with
        {
            FaqPairs = [new FaqPair("What are your hours?", "")],
        };

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
    }

    // ---- Title / ContentHash apply to all source types ----

    [Fact]
    public void MissingTitle_IsInvalid()
    {
        var cmd = BaseCommand(SourceType.Text) with { Title = "", RawText = "content" };

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UploadKnowledgeDocumentCommand.Title));
    }
}
