using FluentAssertions;
using NexConvo.BuildingBlocks.Domain;
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
    public void Role_DefaultsFor_SeedsOwnerAdminSystem_PlusEditableStarters()
    {
        var roles = Role.DefaultsFor(TenantA);

        roles.Select(r => r.Name).Should().BeEquivalentTo("Owner", "Admin", "Member", "Viewer");

        var owner = roles.Single(r => r.Name == "Owner");
        owner.GrantsAll.Should().BeTrue();
        owner.IsSystem.Should().BeTrue();

        roles.Single(r => r.Name == "Admin").IsSystem.Should().BeTrue();
        roles.Single(r => r.Name == "Member").IsSystem.Should().BeFalse();   // editable starter
        roles.Single(r => r.Name == "Viewer").IsSystem.Should().BeFalse();
    }

    [Fact]
    public void Role_CreateCustom_RejectsWildcard()
    {
        var act = () => Role.CreateCustom(TenantA, "Hacker", [Permissions.All]);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Role_SystemRole_CannotBeEdited()
    {
        var owner = Role.DefaultsFor(TenantA).Single(r => r.Name == "Owner");

        owner.Invoking(r => r.Rename("Boss")).Should().Throw<DomainException>();
        owner.Invoking(r => r.UpdatePermissions([Permissions.UsersRead])).Should().Throw<DomainException>();
    }

    [Fact]
    public void User_LocksOut_AfterMaxFailedAttempts_AndUnlocksAfterWindow()
    {
        var now = DateTimeOffset.UtcNow;
        var window = TimeSpan.FromMinutes(15);
        var user = User.Register(TenantA, Email.Create("a@b.com"), "hash", "A B");

        user.IsLockedOut(now).Should().BeFalse();
        for (var i = 0; i < 5; i++)
        {
            user.RegisterFailedLogin(now, maxAttempts: 5, lockoutWindow: window);
        }

        user.IsLockedOut(now).Should().BeTrue();                 // locked at the threshold
        user.IsLockedOut(now + window + TimeSpan.FromSeconds(1)).Should().BeFalse(); // window elapsed
    }

    [Fact]
    public void User_ResetFailedLogins_ClearsCounterAndLock()
    {
        var now = DateTimeOffset.UtcNow;
        var user = User.Register(TenantA, Email.Create("a@b.com"), "hash", "A B");
        for (var i = 0; i < 5; i++)
        {
            user.RegisterFailedLogin(now, maxAttempts: 5, lockoutWindow: TimeSpan.FromMinutes(15));
        }

        user.IsLockedOut(now).Should().BeTrue();
        user.ResetFailedLogins();

        user.IsLockedOut(now).Should().BeFalse();
        user.FailedLoginCount.Should().Be(0);
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
