using Microsoft.EntityFrameworkCore;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Domain.Authentication;

namespace NexConvo.Identity.Application.Common;

/// <summary>
/// Evicts a user's sessions on a credential/2FA change: revokes every active refresh token and
/// consumes any outstanding 2FA challenge token. Used by password change and 2FA enable/disable so
/// a stolen or pre-existing session can't outlive the security event. The caller owns SaveChanges.
/// </summary>
internal static class SessionRevocation
{
    public static async Task RevokeAllAsync(
        IIdentityDbContext db, Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var activeTokens = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in activeTokens)
        {
            token.Revoke(now);
        }

        var pendingChallenges = await db.OneTimeTokens
            .Where(o => o.UserId == userId
                && o.Purpose == OneTimeTokenPurpose.TwoFactorChallenge
                && o.ConsumedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var challenge in pendingChallenges)
        {
            challenge.Consume(now);
        }
    }
}
