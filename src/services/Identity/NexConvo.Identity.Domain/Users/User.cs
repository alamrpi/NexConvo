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
    public int FailedLoginCount { get; private set; }
    public DateTimeOffset? LockoutEndsAt { get; private set; }
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

    public void Deactivate() => Status = UserStatus.Disabled;

    public void Reactivate() => Status = UserStatus.Active;

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

    /// <summary>True while a brute-force lockout is in effect — logins are refused even with the
    /// correct password until <see cref="LockoutEndsAt"/> passes.</summary>
    public bool IsLockedOut(DateTimeOffset now) => LockoutEndsAt is { } until && now < until;

    /// <summary>Records a failed login. Once <paramref name="maxAttempts"/> consecutive failures
    /// accrue, the account is locked for <paramref name="lockoutWindow"/> and the counter resets
    /// (the lock itself, not the counter, blocks further attempts).</summary>
    public void RegisterFailedLogin(DateTimeOffset now, int maxAttempts, TimeSpan lockoutWindow)
    {
        if (IsLockedOut(now))
        {
            return;
        }

        FailedLoginCount++;
        if (FailedLoginCount >= maxAttempts)
        {
            LockoutEndsAt = now + lockoutWindow;
            FailedLoginCount = 0;
        }
    }

    /// <summary>Clears the failure counter and any lockout — call on a successful authentication.</summary>
    public void ResetFailedLogins()
    {
        FailedLoginCount = 0;
        LockoutEndsAt = null;
    }
}
