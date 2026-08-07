using FluentAssertions;
using NexConvo.Knowledge.Application.Common.Interfaces;
using NexConvo.Knowledge.Infrastructure.TextExtraction;

namespace NexConvo.Knowledge.Infrastructure.Tests;

public class TextExtractorFactoryTests
{
    private readonly TextExtractorFactory _sut = new(
        new PdfTextExtractor(),
        new DocxTextExtractor(),
        new PlainTextExtractor());

    [Theory]
    [InlineData("application/pdf", null, KnowledgeSourceFormat.Pdf)]
    [InlineData(null, "report.pdf", KnowledgeSourceFormat.Pdf)]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document", null, KnowledgeSourceFormat.Docx)]
    [InlineData(null, "report.docx", KnowledgeSourceFormat.Docx)]
    [InlineData("text/plain", null, KnowledgeSourceFormat.PlainText)]
    [InlineData(null, "notes.txt", KnowledgeSourceFormat.PlainText)]
    [InlineData(null, "notes.md", KnowledgeSourceFormat.PlainText)]
    [InlineData(null, "data.csv", KnowledgeSourceFormat.PlainText)]
    [InlineData(null, null, KnowledgeSourceFormat.PlainText)]
    [InlineData("application/octet-stream", "unknown.xyz", KnowledgeSourceFormat.PlainText)]
    public void ResolveFormat_FromContentTypeOrFileName_ReturnsExpectedFormat(
        string? contentType, string? fileName, KnowledgeSourceFormat expected)
    {
        var result = _sut.ResolveFormat(contentType, fileName);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(KnowledgeSourceFormat.Pdf, typeof(PdfTextExtractor))]
    [InlineData(KnowledgeSourceFormat.Docx, typeof(DocxTextExtractor))]
    [InlineData(KnowledgeSourceFormat.PlainText, typeof(PlainTextExtractor))]
    public void GetExtractor_ForEachFormat_ReturnsMatchingExtractor(KnowledgeSourceFormat format, Type expectedType)
    {
        var extractor = _sut.GetExtractor(format);

        extractor.Should().BeOfType(expectedType);
    }
}
