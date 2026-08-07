using System.Diagnostics;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Application.Health;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.Integrations.Application.Features.S3Config;

namespace NexConvo.Integrations.Infrastructure.ExternalServices;

/// <summary>
/// Live reachability+auth probe for a workspace's S3-compatible bucket (Standard 22: test-then-save).
/// Uses <see cref="IAmazonS3.ListObjectsV2Async"/> with MaxKeys=1 as the probe — the SDK exposes no
/// direct HeadBucket call, and ListObjectsV2 also works against S3-compatible endpoints (R2/MinIO/Spaces).
/// The client factory is injected so tests can supply a fake/mock without any live AWS credentials;
/// the production factory (built in Task 6) constructs the real <see cref="AmazonS3Client"/> from
/// <see cref="S3TestInput"/> per-call and never caches decrypted secrets. Never logs the secret key.
/// </summary>
internal sealed class S3ConnectionTester : IConnectionTester<S3TestInput>
{
    private readonly Func<S3TestInput, IAmazonS3> _clientFactory;
    private readonly ILogger<S3ConnectionTester>? _logger;

    public S3ConnectionTester(Func<S3TestInput, IAmazonS3> clientFactory, ILogger<S3ConnectionTester>? logger = null)
    {
        _clientFactory = clientFactory;
        _logger = logger;
    }

    public string IntegrationKind => "s3";

    public async Task<ConnectionHealth> TestAsync(S3TestInput input, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var client = _clientFactory(input);
            await client.ListObjectsV2Async(
                new ListObjectsV2Request { BucketName = input.BucketName, MaxKeys = 1 }, ct);
            return ConnectionHealth.Healthy("Bucket reachable", (int)sw.ElapsedMilliseconds);
        }
        catch (AmazonS3Exception ex)
        {
            _logger?.LogWarning("S3 connection test failed: {Status}", ex.StatusCode);
            return ConnectionHealth.Failed(
                $"S3 error: {ex.ErrorCode ?? ex.StatusCode.ToString()}", (int)sw.ElapsedMilliseconds);
        }
        catch (Exception)
        {
            return ConnectionHealth.Failed("Could not reach S3. Check bucket, region, and endpoint.", (int)sw.ElapsedMilliseconds);
        }
    }
}
