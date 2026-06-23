using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Identity.Application.Abstractions;

namespace NexConvo.Identity.Application.Invitations;

public sealed record InvitationDto(Guid Id, string Email, string RoleName, DateTimeOffset ExpiresAt);

/// <summary>Pending (not yet accepted, not expired) invitations for the current tenant.</summary>
public sealed record ListPendingInvitationsQuery : IRequest<Result<IReadOnlyList<InvitationDto>>>;

public sealed class ListPendingInvitationsQueryHandler(IIdentityDbContext db, IClock clock)
    : IRequestHandler<ListPendingInvitationsQuery, Result<IReadOnlyList<InvitationDto>>>
{
    public async Task<Result<IReadOnlyList<InvitationDto>>> Handle(
        ListPendingInvitationsQuery query, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        // RLS scopes both tables to the current tenant; join to resolve the role name.
        var items = await (
            from invite in db.Invitations
            join role in db.Roles on invite.RoleId equals role.Id
            where invite.AcceptedAt == null && invite.ExpiresAt > now
            orderby invite.ExpiresAt
            select new InvitationDto(invite.Id, invite.Email.Value, role.Name, invite.ExpiresAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<InvitationDto>>(items);
    }
}
