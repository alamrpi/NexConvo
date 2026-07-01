using FluentAssertions;
using NexConvo.Identity.Application.Authentication;
using NexConvo.Identity.Application.Authentication.Login;
using NexConvo.Identity.Application.Authentication.Signup;
using NexConvo.Identity.Domain.Roles;

namespace NexConvo.Identity.Tests.Application;

public sealed class ValidatorAndProjectionTests
{
    private static readonly Guid TenantA = Guid.NewGuid();

    [Theory]
    [InlineData("Acme Inc", "acme", "owner@acme.com", "password123", "Jane O", true)]
    [InlineData("", "acme", "owner@acme.com", "password123", "Jane O", false)]          // no name
    [InlineData("Acme", "ab", "owner@acme.com", "password123", "Jane O", false)]        // slug too short
    [InlineData("Acme", "acme", "not-email", "password123", "Jane O", false)]           // bad email
    [InlineData("Acme", "acme", "owner@acme.com", "short", "Jane O", false)]            // weak password
    public void SignupValidator_EnforcesRules(
        string name, string slug, string email, string password, string fullName, bool expectedValid)
    {
        var result = new SignupCommandValidator()
            .Validate(new SignupCommand(name, slug, email, password, fullName));

        result.IsValid.Should().Be(expectedValid);
    }

    [Fact]
    public void LoginValidator_RequiresAllThreeFields()
    {
        new LoginCommandValidator().Validate(new LoginCommand("", "", "")).IsValid.Should().BeFalse();
        new LoginCommandValidator().Validate(new LoginCommand("acme", "a@b.com", "pw")).IsValid.Should().BeTrue();
    }

    [Fact]
    public void RoleProjection_OwnerCollapsesToWildcard()
    {
        var roles = Role.DefaultsFor(TenantA);
        var owner = roles.Single(r => r.Name == "Owner");

        var (names, permissions) = RoleProjection.From([owner]);

        names.Should().ContainSingle().Which.Should().Be("Owner");
        permissions.Should().ContainSingle().Which.Should().Be(Permissions.All);
    }

    [Fact]
    public void RoleProjection_NonOwner_UnionsDistinctPermissions()
    {
        var roles = Role.DefaultsFor(TenantA);
        var nonOwner = roles.Where(r => r.Name != "Owner").ToList();

        var (names, permissions) = RoleProjection.From(nonOwner);

        names.Should().BeEquivalentTo("Admin", "Member", "Viewer");
        permissions.Should().NotContain(Permissions.All);
        permissions.Should().Contain(Permissions.LeadsRead);
        permissions.Should().OnlyHaveUniqueItems();
    }
}
