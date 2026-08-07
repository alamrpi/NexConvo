using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FluentAssertions;
using NexConvo.Knowledge.Infrastructure.TextExtraction;

namespace NexConvo.Knowledge.Infrastructure.Tests;

public class DocxTextExtractorTests
{
    private readonly DocxTextExtractor _sut = new();

    [Fact]
    public async Task ExtractTextAsync_MinimalOnePageDocx_ReturnsParagraphText()
    {
        await using var docxStream = BuildMinimalDocx("Hello from a test DOCX document.");

        var result = await _sut.ExtractTextAsync(docxStream, CancellationToken.None);

        result.Should().Contain("Hello from a test DOCX document.");
    }

    private static MemoryStream BuildMinimalDocx(string text)
    {
        var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());
            var paragraph = body.AppendChild(new Paragraph());
            var run = paragraph.AppendChild(new Run());
            run.AppendChild(new Text(text));
            mainPart.Document.Save();
        }

        stream.Position = 0;
        return stream;
    }
}
