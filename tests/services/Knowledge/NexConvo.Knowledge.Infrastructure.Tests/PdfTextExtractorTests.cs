using FluentAssertions;
using NexConvo.Knowledge.Infrastructure.TextExtraction;
using UglyToad.PdfPig.Writer;

namespace NexConvo.Knowledge.Infrastructure.Tests;

public class PdfTextExtractorTests
{
    private readonly PdfTextExtractor _sut = new();

    [Fact]
    public async Task ExtractTextAsync_MinimalOnePagePdf_ReturnsPageText()
    {
        await using var pdfStream = BuildMinimalPdf("Hello from a test PDF document.");

        var result = await _sut.ExtractTextAsync(pdfStream, CancellationToken.None);

        result.Should().Contain("Hello from a test PDF document.");
    }

    private static MemoryStream BuildMinimalPdf(string text)
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
        var font = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);
        page.AddText(text, 12, new UglyToad.PdfPig.Core.PdfPoint(25, 700), font);

        var bytes = builder.Build();
        return new MemoryStream(bytes);
    }
}
