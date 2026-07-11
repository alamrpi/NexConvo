using Microsoft.Extensions.Logging;
using NexConvo.Knowledge.Application.Common.Interfaces;

namespace NexConvo.Knowledge.Infrastructure.ExternalServices;

/// <summary>
/// Resilient (AddNexConvoResilience()) fetch of a Url-source document's bytes. The SSRF allow-list
/// (DNS resolution + private-range rejection) already ran in the handler before this is called; this
/// class enforces the remaining Url-source guards: https-only, an allow-listed response content-type,
/// and a hard cap on response size so a malicious/misconfigured host can't exhaust memory.
/// </summary>
internal sealed class UrlContentFetcher : IUrlContentFetcher
{
    private const long MaxResponseBytes = 10 * 1024 * 1024; // 10 MB cap (Slice 3 plan)

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/html",
        "text/plain",
        "text/markdown",
        "text/csv",
        "application/pdf",
        "application/xml",
        "application/json",
        "application/octet-stream",
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<UrlContentFetcher> _logger;

    public UrlContentFetcher(IHttpClientFactory httpClientFactory, ILogger<UrlContentFetcher> logger)
    {
        _httpClient = httpClientFactory.CreateClient(KnowledgeHttpClientNames.UrlFetch);
        _logger = logger;
    }

    public async Task<FetchedUrlContent> FetchAsync(Uri url, CancellationToken cancellationToken)
    {
        if (url.Scheme != Uri.UriSchemeHttps)
            throw new NotSupportedException("Only https:// URLs can be fetched for Url-source knowledge documents.");

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType is not null && !AllowedContentTypes.Contains(contentType))
        {
            throw new NotSupportedException(
                $"Content-Type '{contentType}' is not permitted for Url-source knowledge documents.");
        }

        var declaredLength = response.Content.Headers.ContentLength;
        if (declaredLength is > MaxResponseBytes)
        {
            throw new NotSupportedException(
                $"Response exceeds the {MaxResponseBytes} byte cap for Url-source knowledge documents.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var readBuffer = new byte[81920];
        long totalRead = 0;
        int read;
        while ((read = await stream.ReadAsync(readBuffer, cancellationToken)) > 0)
        {
            totalRead += read;
            if (totalRead > MaxResponseBytes)
            {
                throw new NotSupportedException(
                    $"Response exceeds the {MaxResponseBytes} byte cap for Url-source knowledge documents.");
            }

            await buffer.WriteAsync(readBuffer.AsMemory(0, read), cancellationToken);
        }

        _logger.LogInformation(
            "Fetched {ByteCount} bytes from Url-source host {Host}", buffer.Length, url.Host);

        return new FetchedUrlContent(buffer.ToArray(), contentType);
    }
}

/// <summary>Named HttpClient identifiers used by Knowledge.Infrastructure's resilient clients.</summary>
internal static class KnowledgeHttpClientNames
{
    public const string UrlFetch = "KnowledgeUrlFetch";
}
