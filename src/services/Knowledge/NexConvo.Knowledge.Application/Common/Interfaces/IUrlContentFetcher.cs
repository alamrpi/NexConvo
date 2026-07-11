namespace NexConvo.Knowledge.Application.Common.Interfaces;

/// <summary>Raw bytes fetched from a Url-source, plus the response content-type for extractor resolution.</summary>
/// <param name="Content">The fetched bytes — capped at the ingestion pipeline's size limit.</param>
/// <param name="ContentType">The response's Content-Type header, used to resolve the right <see cref="ITextExtractor"/>.</param>
public sealed record FetchedUrlContent(byte[] Content, string? ContentType);

/// <summary>
/// Fetches a knowledge document's source bytes from a remote URL. The SSRF allow-list guard (DNS
/// resolution + private/link-local range check) runs in the handler BEFORE this is called — this
/// abstraction assumes the URL has already been cleared and focuses purely on the resilient fetch
/// (Polly via AddNexConvoResilience()), https-only enforcement, and the response size cap.
/// </summary>
public interface IUrlContentFetcher
{
    Task<FetchedUrlContent> FetchAsync(Uri url, CancellationToken cancellationToken);
}
