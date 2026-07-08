using FluentAssertions;
using MockQueryable.NSubstitute;
using NSubstitute;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Application.Users;
using NexConvo.Identity.Domain.Roles;
using NexConvo.Identity.Domain.Users;
using NexConvo.Identity.Domain.ValueObjects;

namespace NexConvo.Identity.Tests.Application;

/// <summary>
/// Covers the internal (shared-secret guarded) recipients lookup the Notification service calls to
/// find who to email on an integration health failure (Slice 6, Standard 22d). The caller is NOT
/// logged into the target tenant, so the handler must filter by TenantId explicitly rather than
/// relying on RLS (mirrors the Slice-5 owner-connection sweep's explicit-TenantId approach).
/// </summary>
public sealed class GetHealthAlertRecipientsQueryHandlerTests
{
    private readonly IIdentityDbContext _db = Substitute.For<IIdentityDbContext>();
    private readonly IAmbientTenantSetter _tenantSetter = Substitute.For<IAmbientTenantSetter>();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _otherTenantId = Guid.NewGuid();

    [Fact]
    public async Task Handle_ReturnsActiveOwnerAndAdmin_ExcludingInactiveMemberAndOtherTenant()
    {
        var ownerRole = Role.DefaultsFor(_tenantId).Single(r => r.Name == "Owner");
        var adminRole = Role.DefaultsFor(_tenantId).Single(r => r.Name == "Admin");
        var memberRole = Role.DefaultsFor(_tenantId).Single(r => r.Name == "Member");
        var otherTenantOwnerRole = Role.DefaultsFor(_otherTenantId).Single(r => r.Name == "Owner");

        var activeOwner = User.Register(_tenantId, Email.Create("owner@acme.com"), "hash", "Jane Owner");
        var activeAdmin = User.Register(_tenantId, Email.Create("admin@acme.com"), "hash", "Alan Admin");
        var activeMember = User.Register(_tenantId, Email.Create("member@acme.com"), "hash", "Mo Member");
        var inactiveOwner = User.Register(_tenantId, Email.Create("ghost@acme.com"), "hash", "Ghost Owner");
        inactiveOwner.Deactivate();
        var otherTenantOwner = User.Register(_otherTenantId, Email.Create("owner@other.com"), "hash", "Other Owner");

        var userRoles = new List<UserRole>
        {
            new(_tenantId, activeOwner.Id, ownerRole.Id),
            new(_tenantId, activeAdmin.Id, adminRole.Id),
            new(_tenantId, activeMember.Id, memberRole.Id),
            new(_tenantId, inactiveOwner.Id, ownerRole.Id),
            new(_otherTenantId, otherTenantOwner.Id, otherTenantOwnerRole.Id),
        };

        var users = new List<User> { activeOwner, activeAdmin, activeMember, inactiveOwner, otherTenantOwner };
        var roles = new List<Role> { ownerRole, adminRole, memberRole, otherTenantOwnerRole };

        var rolesDbSet = roles.AsQueryable().BuildMockDbSet();
        var userRolesDbSet = userRoles.AsQueryable().BuildMockDbSet();
        var usersDbSet = users.AsQueryable().BuildMockDbSet();
        _db.Roles.Returns(rolesDbSet);
        _db.UserRoles.Returns(userRolesDbSet);
        _db.Users.Returns(usersDbSet);

        var handler = new GetHealthAlertRecipientsQueryHandler(_db, _tenantSetter);

        var result = await handler.Handle(new GetHealthAlertRecipientsQuery(_tenantId), CancellationToken.None);

        result.Should().BeEquivalentTo(
        [
            new RecipientDto("Jane Owner", "owner@acme.com"),
            new RecipientDto("Alan Admin", "admin@acme.com"),
        ]);
        _tenantSetter.Received(1).SetTenant(_tenantId);
    }

    [Fact]
    public async Task Handle_DedupesByEmail_WhenAUserHoldsBothOwnerAndAdminRoles()
    {
        var ownerRole = Role.DefaultsFor(_tenantId).Single(r => r.Name == "Owner");
        var adminRole = Role.DefaultsFor(_tenantId).Single(r => r.Name == "Admin");

        var dualRoleUser = User.Register(_tenantId, Email.Create("dual@acme.com"), "hash", "Dual Role");

        var userRoles = new List<UserRole>
        {
            new(_tenantId, dualRoleUser.Id, ownerRole.Id),
            new(_tenantId, dualRoleUser.Id, adminRole.Id),
        };

        var rolesDbSet = new List<Role> { ownerRole, adminRole }.AsQueryable().BuildMockDbSet();
        var userRolesDbSet = userRoles.AsQueryable().BuildMockDbSet();
        var usersDbSet = new List<User> { dualRoleUser }.AsQueryable().BuildMockDbSet();
        _db.Roles.Returns(rolesDbSet);
        _db.UserRoles.Returns(userRolesDbSet);
        _db.Users.Returns(usersDbSet);

        var handler = new GetHealthAlertRecipientsQueryHandler(_db, _tenantSetter);

        var result = await handler.Handle(new GetHealthAlertRecipientsQuery(_tenantId), CancellationToken.None);

        result.Should().ContainSingle().Which.Should().Be(new RecipientDto("Dual Role", "dual@acme.com"));
        _tenantSetter.Received(1).SetTenant(_tenantId);
    }

    [Fact]
    public async Task Handle_ReturnsEmpty_WhenTenantHasNoActiveOwnerOrAdmin()
    {
        var ownerRole = Role.DefaultsFor(_tenantId).Single(r => r.Name == "Owner");

        var rolesDbSet = new List<Role> { ownerRole }.AsQueryable().BuildMockDbSet();
        var userRolesDbSet = new List<UserRole>().AsQueryable().BuildMockDbSet();
        var usersDbSet = new List<User>().AsQueryable().BuildMockDbSet();
        _db.Roles.Returns(rolesDbSet);
        _db.UserRoles.Returns(userRolesDbSet);
        _db.Users.Returns(usersDbSet);

        var handler = new GetHealthAlertRecipientsQueryHandler(_db, _tenantSetter);

        var result = await handler.Handle(new GetHealthAlertRecipientsQuery(_tenantId), CancellationToken.None);

        result.Should().BeEmpty();
        _tenantSetter.Received(1).SetTenant(_tenantId);
    }
}
