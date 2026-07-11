namespace NexConvo.Knowledge.Application.Common.Interfaces;

/// <summary>
/// Thin abstraction over the workspace's S3-compatible bucket used to persist and later retrieve
/// raw source bytes for knowledge-base ingestion (File/Url/Text/Faq sources all converge on this
/// so the ingestion job always downloads from S3, never re-reads client-supplied bytes).
/// Credentials are read from the tenant's cached+decrypted S3 config at call time and are never
/// logged or cached in plaintext (Standard 13/15).
/// </summary>
public interface IS3StorageService
{
    Task PutObjectAsync(Guid tenantId, string key, Stream content, string contentType, CancellationToken ct);

    Task<Stream> GetObjectAsync(Guid tenantId, string key, CancellationToken ct);
}
