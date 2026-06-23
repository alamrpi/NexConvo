using NexConvo.BuildingBlocks.Domain;
using NexConvo.Identity.Domain.ValueObjects;

namespace NexConvo.Identity.Domain.Invitations;

/// <summary>
/// A pending invitation for an email to join a tenant with a given role. Accepting it
/// provisions the user. Only the token hash is stored; the link carries the plaintext.
/// </summary>
public sealed class Invitation : BaseAggregateRoot
{
    public Email Email { get; private set; } = null!;
    public Guid RoleId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public Guid InvitedByUserId { get; private set; }

    private Invitation() { } // EF

    public static Invitation Issue(
        Guid tenantId, Email email, Guid roleId, string tokenHash, DateTimeOffset expiresAt, Guid invitedByUserId)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException("Token hash is required.");
        }

        return new Invitation
        {
            TenantId = tenantId,
            Email = email,
            RoleId = roleId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            InvitedByUserId = invitedByUserId,
        };
    }

    public bool IsPending(DateTimeOffset now) => AcceptedAt is null && now < ExpiresAt;

    public void Accept(DateTimeOffset now) => AcceptedAt ??= now;
}
