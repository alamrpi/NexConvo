using System.Text;
using FluentAssertions;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Infrastructure.Security;

namespace NexConvo.Identity.Tests.Infrastructure;

/// <summary>
/// Validates the hand-rolled TOTP against the published RFC 6238 test vectors
/// (SHA-1, seed = ASCII "12345678901234567890"), so the no-package implementation is provably correct.
/// </summary>
public sealed class TotpServiceTests
{
    private static readonly byte[] Seed = Encoding.ASCII.GetBytes("12345678901234567890");
    private const string SeedBase32 = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    [Theory]
    [InlineData(1L, "287082")]          // T = 59s
    [InlineData(37037036L, "081804")]   // T = 1111111109s
    [InlineData(41152263L, "005924")]   // T = 1234567890s
    public void ComputeCode_MatchesRfc6238Vectors(long counter, string expected) =>
        TotpService.ComputeCode(Seed, counter).Should().Be(expected);

    [Fact]
    public void Base32_RoundTrips_AndMatchesKnownEncoding()
    {
        Base32.Encode(Seed).Should().Be(SeedBase32);
        Base32.Decode(SeedBase32).Should().Equal(Seed);
    }

    [Fact]
    public void Verify_AcceptsCurrentCode_RejectsWrongCode()
    {
        var totp = new TotpService(new FixedClock(DateTimeOffset.UnixEpoch.AddSeconds(59)));

        totp.Verify(SeedBase32, "287082").Should().BeTrue();
        totp.Verify(SeedBase32, "000000").Should().BeFalse();
        totp.Verify(SeedBase32, "12345").Should().BeFalse();   // wrong length
    }

    [Fact]
    public void Verify_ToleratesOneStepOfDrift()
    {
        // Code from the previous 30s window still validates (±1 step).
        var totp = new TotpService(new FixedClock(DateTimeOffset.UnixEpoch.AddSeconds(59 + 30)));
        totp.Verify(SeedBase32, "287082").Should().BeTrue();
    }

    [Fact]
    public void GenerateSecret_IsDecodableBase32()
    {
        var totp = new TotpService(new FixedClock(DateTimeOffset.UnixEpoch));
        var secret = totp.GenerateSecret();

        secret.Should().MatchRegex("^[A-Z2-7]+$");
        Base32.Decode(secret).Length.Should().Be(20);
    }
}
