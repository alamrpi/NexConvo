using NexConvo.BuildingBlocks.Domain;

namespace NexConvo.Identity.Domain.Authentication;

public enum OneTimeTokenPurpose
{
    EmailVerification = 0,
    PasswordReset = 1,
    TwoFactorChallenge = 2,
}

/// <summary>
/// A single-use, expiring token tied to a user (email verification / password reset).
/// Only the SHA-256 hash of the opaque token is stored; the link carries the plaintext.
/// </summary>
public sealed class OneTimeToken : BaseAggregateRoot
{
    public Guid UserId { get; private set; }
    public OneTimeTokenPurpose Purpose { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }

    private OneTimeToken() { } // EF

    public static OneTimeToken Issue(
        Guid tenantId, Guid userId, OneTimeTokenPurpose purpose, string tokenHash, DateTimeOffset expiresAt)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException("Token hash is required.");
        }

        return new OneTimeToken
        {
            TenantId = tenantId,
            UserId = userId,
            Purpose = purpose,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
        };
    }

    public bool IsValid(DateTimeOffset now) => ConsumedAt is null && now < ExpiresAt;

    public void Consume(DateTimeOffset now)
    {
        if (ConsumedAt is null)
        {
            ConsumedAt = now;
        }
    }
}
