using System.Text;
using NexConvo.Knowledge.Application.Common.Interfaces;

namespace NexConvo.Knowledge.Infrastructure.TextExtraction;

/// <summary>
/// Reads raw UTF-8 text as-is — used for TXT/MD/CSV sources, and for the synthetic
/// Text/Faq source types whose "extraction" is simply decoding the stored bytes.
/// </summary>
public sealed class PlainTextExtractor : ITextExtractor
{
    public IReadOnlyCollection<KnowledgeSourceFormat> SupportedFormats { get; } = [KnowledgeSourceFormat.PlainText];

    public async Task<string> ExtractTextAsync(Stream content, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
