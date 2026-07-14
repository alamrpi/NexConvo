using System.Text;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using NexConvo.BuildingBlocks.Application.Security;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.Contracts.Events.Integrations;
using NexConvo.Knowledge.Infrastructure.ExternalServices;
using NSubstitute;

namespace NexConvo.Knowledge.Infrastructure.Tests;

public class S3StorageServiceTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly IDistributedCache _cache = Substitute.For<IDistributedCache>();
    private readonly IAesEncryptionService _encryption = Substitute.For<IAesEncryptionService>();
    private readonly IAmazonS3 _s3Client = Substitute.For<IAmazonS3>();

    private S3StorageService BuildSut()
    {
        var config = new S3ConfigUpdatedEvent(
            _tenantId, "bucket", "us-east-1", CustomEndpoint: "http://minio:9000", PathPrefix: null,
            IsActive: true, EncryptedAccessKeyId: "enc-key", EncryptedSecretAccessKey: "enc-secret",
            LastTestStatus: ConnectionStatus.Healthy.ToString(), LastTestedAt: DateTimeOffset.UtcNow);

        // IDistributedCache.GetStringAsync is an extension method over GetAsync(byte[]) — mock
        // the real interface member, not the extension, or NSubstitute can't intercept the call.
        _cache.GetAsync($"S3Config:{_tenantId}", Arg.Any<CancellationToken>())
            .Returns(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(config)));
        _encryption.Decrypt(Arg.Any<string>()).Returns(ci => ci.Arg<string>());

        return new S3StorageService(_cache, _encryption, _ => _s3Client, NullLogger<S3StorageService>.Instance);
    }

    /// <summary>
    /// A minimal stand-in for the AWS SDK's HashStream: forward-only, throws on Position/Seek,
    /// exactly like the real wrapper the SDK puts around GetObjectResponse.ResponseStream.
    /// </summary>
    private sealed class NonSeekableStream(byte[] data) : Stream
    {
        private readonly MemoryStream _inner = new(data);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException("HashStream does not support seeking");
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => _inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => _inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { }
    }

    [Fact]
    public async Task GetObjectAsync_NonSeekableSdkResponseStream_ReturnsASeekableStream()
    {
        // Regression guard: the AWS SDK wraps GetObjectResponse.ResponseStream in a
        // checksum-validating HashStream whose Position getter throws NotSupportedException on
        // EVERY S3-compatible endpoint (AWS, MinIO, R2, ...) — not something specific to this
        // local dev setup. PdfPig (and any format parser needing random access, e.g. to read a
        // PDF's trailing cross-reference table) requires CanSeek == true, so GetObjectAsync must
        // buffer the response rather than return the SDK's stream as-is.
        var contentBytes = Encoding.UTF8.GetBytes("pdf-like source bytes");
        _s3Client
            .GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new GetObjectResponse { ResponseStream = new NonSeekableStream(contentBytes) }));

        var sut = BuildSut();

        await using var result = await sut.GetObjectAsync(_tenantId, "some/key.pdf", CancellationToken.None);

        result.CanSeek.Should().BeTrue();
        result.Position = 0; // would throw if this were still the SDK's raw HashStream-wrapped stream
        using var reader = new StreamReader(result, leaveOpen: true);
        (await reader.ReadToEndAsync()).Should().Be("pdf-like source bytes");
    }

    [Fact]
    public async Task GetObjectAsync_DisposesTheS3Client_AfterBufferingCompletes()
    {
        _s3Client
            .GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new GetObjectResponse
            {
                ResponseStream = new NonSeekableStream(Encoding.UTF8.GetBytes("x")),
            }));

        var sut = BuildSut();

        await using (await sut.GetObjectAsync(_tenantId, "some/key.pdf", CancellationToken.None))
        {
        }

        _s3Client.Received(1).Dispose();
    }
}
