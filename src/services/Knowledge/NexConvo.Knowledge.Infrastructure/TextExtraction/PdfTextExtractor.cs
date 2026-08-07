using System.Text;
using NexConvo.Knowledge.Application.Common.Interfaces;
using UglyToad.PdfPig;

namespace NexConvo.Knowledge.Infrastructure.TextExtraction;

/// <summary>Extracts plain text from a PDF's pages, in order, via PdfPig.</summary>
public sealed class PdfTextExtractor : ITextExtractor
{
    public IReadOnlyCollection<KnowledgeSourceFormat> SupportedFormats { get; } = [KnowledgeSourceFormat.Pdf];

    public Task<string> ExtractTextAsync(Stream content, CancellationToken cancellationToken)
    {
        // PdfPig's document open/read is synchronous; keep this method awaitable so callers
        // (resolved via the shared ITextExtractor abstraction) never care which extractor
        // is I/O-bound and which is CPU-bound.
        using var document = PdfDocument.Open(content);
        var text = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            text.AppendLine(page.Text);
        }

        return Task.FromResult(text.ToString());
    }
}
