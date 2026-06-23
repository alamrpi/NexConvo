using NexConvo.BuildingBlocks.Domain;
using NexConvo.Identity.Domain.Events;
using NexConvo.Identity.Domain.ValueObjects;

namespace NexConvo.Identity.Domain.Users;

public enum UserStatus
{
    Active = 0,
    Disabled = 1,
}

/// <summary>A tenant-scoped user (RLS-protected). Holds only a password hash, never the password.</summary>
public sealed class User : BaseAggregateRoot
{
    private readonly List<UserRole> _roles = [];

    public Email Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string FullName { get; private set; } = null!;
    public UserStatus Status { get; private set; }
    public DateTimeOffset? EmailVerifiedAt { get; private set; }
    public IReadOnlyCollection<UserRole> Roles => _roles.AsReadOnly();

    private User() { } // EF

    public static User Register(Guid tenantId, Email email, string passwordHash, string fullName)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("Password hash is required.");
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainException("Full name is required.");
        }

        var user = new User
        {
            TenantId = tenantId,
            Email = email,
            PasswordHash = passwordHash,
            FullName = fullName.Trim(),
            Status = UserStatus.Active,
        };

        user.RaiseDomainEvent(new UserRegisteredDomainEvent(user.Id, tenantId, email.Value, user.FullName));
        return user;
    }

    public void AssignRole(Guid roleId)
    {
        if (_roles.Exists(r => r.RoleId == roleId))
        {
            return;
        }

        _roles.Add(new UserRole(TenantId, Id, roleId));
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("Password hash is required.");
        }

        PasswordHash = passwordHash;
    }

    public bool IsActive => Status == UserStatus.Active;

    public bool IsEmailVerified => EmailVerifiedAt is not null;

    public void MarkEmailVerified(DateTimeOffset at) => EmailVerifiedAt ??= at;
}
