namespace NexConvo.Knowledge.Application.Common.Interfaces;

/// <summary>
/// Extracts plain text from a single document format's raw bytes. Pure conversion — no
/// persistence, no tenant context. Implementations never log document content (Standard 9/15).
/// </summary>
public interface ITextExtractor
{
    /// <summary>The document formats this extractor can handle.</summary>
    IReadOnlyCollection<KnowledgeSourceFormat> SupportedFormats { get; }

    /// <summary>Reads <paramref name="content"/> fully and returns its extracted plain text.</summary>
    Task<string> ExtractTextAsync(Stream content, CancellationToken cancellationToken);
}

/// <summary>
/// The document formats the ingestion pipeline knows how to turn into plain text.
/// Resolved from a source's content-type/file extension by <see cref="ITextExtractorFactory"/>.
/// Named distinctly from DocumentFormat.OpenXml's own `DocumentFormat` root namespace to avoid
/// a same-name collision in Infrastructure, where that package is referenced.
/// </summary>
public enum KnowledgeSourceFormat
{
    Pdf,
    Docx,
    PlainText,
}

/// <summary>
/// Resolves the right <see cref="ITextExtractor"/> for a given content-type/file name so
/// callers never need to know which concrete extractor handles which format.
/// </summary>
public interface ITextExtractorFactory
{
    /// <summary>
    /// Determines the <see cref="KnowledgeSourceFormat"/> for a source from its MIME content-type
    /// and/or file name extension. Falls back to <see cref="KnowledgeSourceFormat.PlainText"/> when
    /// neither hint is recognized (TXT/MD/CSV all read as raw text).
    /// </summary>
    KnowledgeSourceFormat ResolveFormat(string? contentType, string? fileName);

    /// <summary>Returns the extractor registered for <paramref name="format"/>.</summary>
    ITextExtractor GetExtractor(KnowledgeSourceFormat format);
}
