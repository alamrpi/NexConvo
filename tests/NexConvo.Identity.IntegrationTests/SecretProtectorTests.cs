using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using NexConvo.Identity.Infrastructure.Security;

namespace NexConvo.Identity.IntegrationTests;

public sealed class SecretProtectorTests
{
    [Fact]
    public void Protect_Unprotect_RoundTrips_AndCiphertextDiffersFromPlaintext()
    {
        var provider = DataProtectionProvider.Create("NexConvo.Tests");
        var protector = new DataProtectionSecretProtector(provider);
        const string secret = "re_live_abc123_super_secret_api_key";

        var cipher = protector.Protect(secret);

        cipher.Should().NotBe(secret);
        protector.Unprotect(cipher).Should().Be(secret);
    }
}
