using FluentAssertions;
using NexConvo.BuildingBlocks.Domain;
using NexConvo.Identity.Domain.ValueObjects;

namespace NexConvo.Identity.Tests.Domain;

public sealed class ValueObjectTests
{
    [Theory]
    [InlineData("  User@Example.COM  ", "user@example.com")]
    [InlineData("a@b.co", "a@b.co")]
    public void Email_Create_TrimsAndLowercases(string input, string expected)
    {
        Email.Create(input).Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("missing@domain")]
    public void Email_Create_RejectsInvalid(string input)
    {
        var act = () => Email.Create(input);
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("Acme-Corp", "acme-corp")]
    [InlineData("nex123", "nex123")]
    public void TenantSlug_Create_NormalizesValid(string input, string expected)
    {
        TenantSlug.Create(input).Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("ab")]            // too short
    [InlineData("-bad")]          // leading hyphen
    [InlineData("bad-")]          // trailing hyphen
    [InlineData("dou--ble")]      // double hyphen
    [InlineData("white space")]   // space
    public void TenantSlug_Create_RejectsInvalid(string input)
    {
        var act = () => TenantSlug.Create(input);
        act.Should().Throw<DomainException>();
    }
}
