namespace NexConvo.Chat.Application.Common.Interfaces;

/// <summary>
/// Resolves a public widget token to its owning tenant — the single, tightly-scoped RLS-exempt
/// lookup the anonymous widget path is allowed to perform (Standard 6). The widget presents only an
/// unguessable <c>WidgetToken</c>; we don't yet know the tenant, so RLS cannot be scoped until this
/// resolves. Implemented over a Postgres <c>SECURITY DEFINER</c> function that exposes ONLY the
/// token→tenant lookup (gated on an active Web channel) — the service role gets EXECUTE on that
/// function, never a table read that bypasses RLS. Every subsequent read/stream runs tenant-scoped
/// via <see cref="IChatDbContextFactory.CreateForTenant"/>.
/// </summary>
public interface IWidgetTenantResolver
{
    /// <summary>
    /// Returns the tenant that owns <paramref name="widgetToken"/> and has an active Web widget
    /// channel, or <c>null</c> if the token is unknown, empty, or the Web channel is inactive.
    /// </summary>
    Task<Guid?> ResolveAsync(Guid widgetToken, CancellationToken ct);
}
