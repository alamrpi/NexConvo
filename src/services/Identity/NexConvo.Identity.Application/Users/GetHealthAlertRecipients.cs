using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Domain.Users;

namespace NexConvo.Identity.Application.Users;

/// <summary>An alert recipient's display name and email (Slice 6 — health-failure notifications).</summary>
public sealed record RecipientDto(string Name, string Email);

/// <summary>
/// Resolves a tenant's active Owner+Admin contacts. Called ONLY via the internal, shared-secret
/// guarded endpoint (<see cref="Api.Controllers.InternalController"/>) — the caller (Notification
/// service) is not logged into the target tenant, so there is no JWT/HttpContext to seed
/// <c>app.current_tenant_id</c>. Users/UserRoles/Roles run under FORCE ROW LEVEL SECURITY, so without
/// explicitly setting the ambient tenant, the RLS predicate evaluates against a NULL setting and
/// admits zero rows regardless of the in-app <see cref="TenantId"/> filter. The handler therefore
/// calls <see cref="IAmbientTenantSetter.SetTenant"/> first (same pattern as Login/Refresh) and keeps
/// the explicit TenantId filter as defense in depth (mirrors the Slice-5 owner-connection sweep).
/// </summary>
public sealed record GetHealthAlertRecipientsQuery(Guid TenantId) : IRequest<IReadOnlyList<RecipientDto>>;

public sealed class GetHealthAlertRecipientsQueryHandler(IIdentityDbContext db, IAmbientTenantSetter tenantSetter)
    : IRequestHandler<GetHealthAlertRecipientsQuery, IReadOnlyList<RecipientDto>>
{
    public async Task<IReadOnlyList<RecipientDto>> Handle(
        GetHealthAlertRecipientsQuery query, CancellationToken cancellationToken)
    {
        // Must run before any DB access — there's no JWT here, so this is the only thing that makes
        // the RLS interceptor emit `set_config(app.current_tenant_id, ...)` on the connection.
        tenantSetter.SetTenant(query.TenantId);

        // Pin to the seeded system roles — a tenant could rename/recreate a custom role "Owner"/"Admin".
        var alertRoleIds = await db.Roles
            .Where(r => r.IsSystem && (r.Name == "Owner" || r.Name == "Admin") && r.TenantId == query.TenantId)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        if (alertRoleIds.Count == 0)
        {
            return [];
        }

        var recipients = await (
            from ur in db.UserRoles
            join u in db.Users on ur.UserId equals u.Id
            where alertRoleIds.Contains(ur.RoleId)
                  && u.Status == UserStatus.Active
                  && u.TenantId == query.TenantId
            select new RecipientDto(u.FullName, u.Email.Value))
            .ToListAsync(cancellationToken);

        return recipients
            .GroupBy(r => r.Email, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }
}
