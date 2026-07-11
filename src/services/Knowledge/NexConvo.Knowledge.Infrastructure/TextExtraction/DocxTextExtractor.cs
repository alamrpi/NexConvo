using System.Text;
using DocumentFormat.OpenXml.Packaging;
using NexConvo.Knowledge.Application.Common.Interfaces;

namespace NexConvo.Knowledge.Infrastructure.TextExtraction;

/// <summary>Extracts plain text from a DOCX's body paragraphs, in order, via OpenXml SDK.</summary>
public sealed class DocxTextExtractor : ITextExtractor
{
    public IReadOnlyCollection<KnowledgeSourceFormat> SupportedFormats { get; } = [KnowledgeSourceFormat.Docx];

    public Task<string> ExtractTextAsync(Stream content, CancellationToken cancellationToken)
    {
        using var document = WordprocessingDocument.Open(content, isEditable: false);
        var body = document.MainDocumentPart?.Document.Body;

        if (body is null)
        {
            return Task.FromResult(string.Empty);
        }

        var text = new StringBuilder();
        foreach (var paragraph in body.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            text.AppendLine(paragraph.InnerText);
        }

        return Task.FromResult(text.ToString());
    }
}
