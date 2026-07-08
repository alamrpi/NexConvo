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
/// service) is not logged into the target tenant, so <see cref="TenantId"/> is applied as an
/// EXPLICIT filter rather than relying on RLS (mirrors the Slice-5 owner-connection sweep).
/// </summary>
public sealed record GetHealthAlertRecipientsQuery(Guid TenantId) : IRequest<IReadOnlyList<RecipientDto>>;

public sealed class GetHealthAlertRecipientsQueryHandler(IIdentityDbContext db)
    : IRequestHandler<GetHealthAlertRecipientsQuery, IReadOnlyList<RecipientDto>>
{
    public async Task<IReadOnlyList<RecipientDto>> Handle(
        GetHealthAlertRecipientsQuery query, CancellationToken cancellationToken)
    {
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
