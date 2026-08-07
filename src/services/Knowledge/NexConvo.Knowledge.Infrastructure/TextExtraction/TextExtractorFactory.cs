using NexConvo.Knowledge.Application.Common.Interfaces;

namespace NexConvo.Knowledge.Infrastructure.TextExtraction;

/// <summary>
/// Resolves a <see cref="KnowledgeSourceFormat"/> from a source's content-type/file-name and hands
/// back the matching <see cref="ITextExtractor"/>. Falls back to <see cref="KnowledgeSourceFormat.PlainText"/>
/// when neither hint is recognized, so TXT/MD/CSV (and the synthetic Text/Faq pipeline) always
/// resolve to a working extractor.
/// </summary>
public sealed class TextExtractorFactory : ITextExtractorFactory
{
    private static readonly IReadOnlyDictionary<string, KnowledgeSourceFormat> ContentTypeMap =
        new Dictionary<string, KnowledgeSourceFormat>(StringComparer.OrdinalIgnoreCase)
        {
            ["application/pdf"] = KnowledgeSourceFormat.Pdf,
            ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = KnowledgeSourceFormat.Docx,
            ["text/plain"] = KnowledgeSourceFormat.PlainText,
            ["text/markdown"] = KnowledgeSourceFormat.PlainText,
            ["text/csv"] = KnowledgeSourceFormat.PlainText,
        };

    private static readonly IReadOnlyDictionary<string, KnowledgeSourceFormat> ExtensionMap =
        new Dictionary<string, KnowledgeSourceFormat>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = KnowledgeSourceFormat.Pdf,
            [".docx"] = KnowledgeSourceFormat.Docx,
            [".txt"] = KnowledgeSourceFormat.PlainText,
            [".md"] = KnowledgeSourceFormat.PlainText,
            [".csv"] = KnowledgeSourceFormat.PlainText,
        };

    private readonly IReadOnlyDictionary<KnowledgeSourceFormat, ITextExtractor> _extractorsByFormat;

    public TextExtractorFactory(
        PdfTextExtractor pdfTextExtractor,
        DocxTextExtractor docxTextExtractor,
        PlainTextExtractor plainTextExtractor)
    {
        _extractorsByFormat = new Dictionary<KnowledgeSourceFormat, ITextExtractor>
        {
            [KnowledgeSourceFormat.Pdf] = pdfTextExtractor,
            [KnowledgeSourceFormat.Docx] = docxTextExtractor,
            [KnowledgeSourceFormat.PlainText] = plainTextExtractor,
        };
    }

    public KnowledgeSourceFormat ResolveFormat(string? contentType, string? fileName)
    {
        if (!string.IsNullOrWhiteSpace(contentType) && ContentTypeMap.TryGetValue(contentType, out var byContentType))
        {
            return byContentType;
        }

        var extension = string.IsNullOrWhiteSpace(fileName) ? null : Path.GetExtension(fileName);
        if (!string.IsNullOrEmpty(extension) && ExtensionMap.TryGetValue(extension, out var byExtension))
        {
            return byExtension;
        }

        // Unrecognized/absent hints default to plain text — the safest fallback for the
        // synthetic Text/Faq pipeline and for TXT/MD/CSV sources with no reliable MIME type.
        return KnowledgeSourceFormat.PlainText;
    }

    public ITextExtractor GetExtractor(KnowledgeSourceFormat format) => _extractorsByFormat[format];
}
