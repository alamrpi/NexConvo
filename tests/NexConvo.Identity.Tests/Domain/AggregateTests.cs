using FluentAssertions;
using NexConvo.Identity.Domain.Authentication;
using NexConvo.Identity.Domain.Events;
using NexConvo.Identity.Domain.Roles;
using NexConvo.Identity.Domain.Users;
using NexConvo.Identity.Domain.ValueObjects;

namespace NexConvo.Identity.Tests.Domain;

public sealed class AggregateTests
{
    private static readonly Guid TenantA = Guid.NewGuid();

    [Fact]
    public void User_Register_RaisesEvent_AndStartsWithNoRoles()
    {
        var user = User.Register(TenantA, Email.Create("owner@acme.com"), "hash", "Jane Owner");

        user.TenantId.Should().Be(TenantA);
        user.Roles.Should().BeEmpty();
        user.IsActive.Should().BeTrue();
        user.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<UserRegisteredDomainEvent>();
    }

    [Fact]
    public void User_AssignRole_IsIdempotent()
    {
        var user = User.Register(TenantA, Email.Create("a@b.com"), "hash", "A B");
        var roleId = Guid.NewGuid();

        user.AssignRole(roleId);
        user.AssignRole(roleId);

        user.Roles.Should().ContainSingle(r => r.RoleId == roleId);
    }

    [Fact]
    public void Role_DefaultsFor_SeedsOwnerWithWildcard()
    {
        var roles = Role.DefaultsFor(TenantA);

        roles.Should().HaveCount(3);
        var owner = roles.Single(r => r.Name == "Owner");
        owner.GrantsAll.Should().BeTrue();
        roles.Single(r => r.Name == "Agent").GrantsAll.Should().BeFalse();
    }

    [Fact]
    public void RefreshToken_IsActive_TrueUntilExpiredOrRevoked()
    {
        var now = DateTimeOffset.UtcNow;
        var token = RefreshToken.Issue(TenantA, Guid.NewGuid(), "tokenhash", now.AddDays(14));

        token.IsActive(now).Should().BeTrue();
        token.IsActive(now.AddDays(15)).Should().BeFalse();           // expired

        token.Revoke(now, replacedByHash: "newhash");
        token.IsActive(now).Should().BeFalse();                        // revoked
        token.ReplacedByHash.Should().Be("newhash");
    }

    [Fact]
    public void RefreshToken_Revoke_IsIdempotent()
    {
        var now = DateTimeOffset.UtcNow;
        var token = RefreshToken.Issue(TenantA, Guid.NewGuid(), "h", now.AddDays(1));

        token.Revoke(now, "first");
        token.Revoke(now.AddMinutes(5), "second");

        token.ReplacedByHash.Should().Be("first");
    }
}
