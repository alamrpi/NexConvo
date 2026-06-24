using NexConvo.Identity.Application.Common;

namespace NexConvo.Identity.Application.Abstractions;

/// <summary>
/// Caches the GET /me projection (<see cref="CurrentUserDto"/>) per user with a short TTL to cut
/// DB load on the hot session-hydration path (every dashboard navigation hydrates from /me).
/// Backed by Redis in production. /me is DISPLAY data — authorization is enforced from JWT claims,
/// not this cache — so a brief staleness window is safe; it is invalidated promptly whenever the
/// user's profile, role, 2FA, email-verification, or active status changes.
/// </summary>
public interface ICurrentUserCache
{
    Task<CurrentUserDto?> GetAsync(Guid userId, CancellationToken cancellationToken);

    Task SetAsync(Guid userId, CurrentUserDto value, CancellationToken cancellationToken);

    Task InvalidateAsync(Guid userId, CancellationToken cancellationToken);
}
