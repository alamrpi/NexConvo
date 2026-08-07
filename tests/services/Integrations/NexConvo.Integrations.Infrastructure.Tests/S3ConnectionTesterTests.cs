using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.Integrations.Application.Features.S3Config;
using NexConvo.Integrations.Infrastructure.ExternalServices;
using NSubstitute;

namespace NexConvo.Integrations.Infrastructure.Tests;

public class S3ConnectionTesterTests
{
    private static S3TestInput Input() => new("bucket", "us-east-1", "ak", "sk", null);

    [Fact]
    public async Task Returns_healthy_on_successful_probe()
    {
        var fake = Substitute.For<IAmazonS3>();
        fake.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>())
            .Returns(new ListObjectsV2Response());
        var tester = new S3ConnectionTester(_ => fake);

        var result = await tester.TestAsync(Input(), default);

        result.Success.Should().BeTrue();
        result.Status.Should().Be(ConnectionStatus.Healthy);
        result.LatencyMs.Should().NotBeNull();
    }

    [Fact]
    public async Task Returns_failed_on_s3_exception()
    {
        var fake = Substitute.For<IAmazonS3>();
        fake.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>())
            .Returns<ListObjectsV2Response>(_ => throw new AmazonS3Exception("Access denied")
            {
                ErrorCode = "AccessDenied",
            });
        var tester = new S3ConnectionTester(_ => fake);

        var result = await tester.TestAsync(Input(), default);

        result.Success.Should().BeFalse();
        result.Status.Should().Be(ConnectionStatus.Failed);
        string.IsNullOrWhiteSpace(result.ErrorMessage).Should().BeFalse();
    }
}
