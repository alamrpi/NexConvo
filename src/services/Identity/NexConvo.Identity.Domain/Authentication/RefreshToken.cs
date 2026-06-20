using NexConvo.BuildingBlocks.Domain;

namespace NexConvo.Identity.Domain.Authentication;

/// <summary>
/// A tenant-scoped refresh token. Only the SHA-256 hash of the opaque token is stored.
/// Rotated on use: the old token is revoked and points to its replacement's hash.
/// </summary>
public sealed class RefreshToken : BaseAggregateRoot
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? ReplacedByHash { get; private set; }

    private RefreshToken() { } // EF

    public static RefreshToken Issue(Guid tenantId, Guid userId, string tokenHash, DateTimeOffset expiresAt)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException("Refresh token hash is required.");
        }

        return new RefreshToken
        {
            TenantId = tenantId,
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
        };
    }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;

    public void Revoke(DateTimeOffset now, string? replacedByHash = null)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = now;
        ReplacedByHash = replacedByHash;
    }
}
