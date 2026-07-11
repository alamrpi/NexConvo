using System.Text.Json;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Application.Security;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.Contracts.Events.Integrations;
using NexConvo.Knowledge.Application.Common.Interfaces;

namespace NexConvo.Knowledge.Infrastructure.ExternalServices;

/// <summary>
/// Thin adapter over the tenant's S3-compatible bucket for knowledge-base source storage.
/// Reads the tenant's S3 config from the Redis cache populated by
/// <see cref="NexConvo.Knowledge.Application.Features.S3Config.EventHandlers.S3ConfigUpdatedEventConsumer"/>,
/// decrypts credentials at use-time only (Standard 13/15 — never logged, never cached plaintext),
/// and builds a fresh <see cref="AmazonS3Client"/> per call via the injected factory — mirroring
/// NexConvo.Integrations.Infrastructure.ExternalServices.S3ConnectionTester's client construction.
/// </summary>
internal sealed class S3StorageService : IS3StorageService
{
    private readonly IDistributedCache _cache;
    private readonly IAesEncryptionService _encryptionService;
    private readonly Func<S3StorageInput, IAmazonS3> _clientFactory;
    private readonly ILogger<S3StorageService> _logger;

    public S3StorageService(
        IDistributedCache cache,
        IAesEncryptionService encryptionService,
        Func<S3StorageInput, IAmazonS3> clientFactory,
        ILogger<S3StorageService> logger)
    {
        _cache = cache;
        _encryptionService = encryptionService;
        _clientFactory = clientFactory;
        _logger = logger;
    }

    public async Task PutObjectAsync(Guid tenantId, string key, Stream content, string contentType, CancellationToken ct)
    {
        var (client, bucketName, objectKey) = await ResolveAsync(tenantId, key, ct);
        using (client)
        {
            await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucketName,
                Key = objectKey,
                InputStream = content,
                ContentType = contentType,
            }, ct);
        }

        _logger.LogInformation(
            "Knowledge source object {ObjectKey} written to S3 for tenant {TenantId}", objectKey, tenantId);
    }

    public async Task<Stream> GetObjectAsync(Guid tenantId, string key, CancellationToken ct)
    {
        var (client, bucketName, objectKey) = await ResolveAsync(tenantId, key, ct);
        try
        {
            var response = await client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = bucketName,
                Key = objectKey,
            }, ct);

            _logger.LogInformation(
                "Knowledge source object {ObjectKey} read from S3 for tenant {TenantId}", objectKey, tenantId);

            return new S3ObjectDisposingStream(response.ResponseStream, client);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private async Task<(IAmazonS3 Client, string BucketName, string ObjectKey)> ResolveAsync(
        Guid tenantId, string key, CancellationToken ct)
    {
        var cacheKey = $"S3Config:{tenantId}";
        var cached = await _cache.GetStringAsync(cacheKey, ct);
        if (string.IsNullOrEmpty(cached))
            throw new ConnectionUnhealthyException("s3", "No S3 configuration is cached for this workspace.");

        var config = JsonSerializer.Deserialize<S3ConfigUpdatedEvent>(cached)
            ?? throw new ConnectionUnhealthyException("s3", "Cached S3 configuration could not be read.");

        if (!config.IsActive || !Enum.TryParse<ConnectionStatus>(config.LastTestStatus, out var status)
            || status != ConnectionStatus.Healthy)
        {
            throw new ConnectionUnhealthyException("s3", "The workspace S3 connection is not healthy.");
        }

        var accessKeyId = _encryptionService.Decrypt(config.EncryptedAccessKeyId);
        var secretAccessKey = _encryptionService.Decrypt(config.EncryptedSecretAccessKey);

        var client = _clientFactory(new S3StorageInput(
            config.BucketName, config.Region, accessKeyId, secretAccessKey, config.CustomEndpoint));

        var objectKey = string.IsNullOrWhiteSpace(config.PathPrefix)
            ? key
            : $"{config.PathPrefix.TrimEnd('/')}/{key}";

        return (client, config.BucketName, objectKey);
    }

    /// <summary>Disposes the per-call <see cref="IAmazonS3"/> client alongside the response stream it owns.</summary>
    private sealed class S3ObjectDisposingStream(Stream inner, IAmazonS3 client) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => inner.Flush();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
                client.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

/// <summary>Credential/target payload used to construct a per-call <see cref="IAmazonS3"/> client for knowledge storage.</summary>
public sealed record S3StorageInput(
    string BucketName,
    string Region,
    string AccessKeyId,
    string SecretAccessKey,
    string? CustomEndpoint);
