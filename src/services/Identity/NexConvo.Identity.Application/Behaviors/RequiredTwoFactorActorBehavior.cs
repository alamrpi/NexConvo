using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Domain;
using NexConvo.BuildingBlocks.Multitenancy;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Application.Common;

namespace NexConvo.Identity.Application.Behaviors;

/// <summary>
/// Blocks <see cref="IRequireVerifiedActor"/> commands when the acting user's workspace requires
/// two-factor authentication and the user hasn't enabled 2FA (soft enforcement, per-tenant —
/// driven by <c>Tenant.RequireTwoFactor</c>, no global flag). Checks live DB state so enabling 2FA
/// takes effect immediately. **Fail-closed**: when the workspace requires 2FA, the actor must be
/// found AND enrolled — a missing/invisible actor row is denied, not allowed. Throws
/// <see cref="TwoFactorRequiredException"/>, mapped to 403 at the API edge.
/// </summary>
public sealed class RequiredTwoFactorActorBehavior<TRequest, TResponse>(IIdentityDbContext db, ITenantContext tenant)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequireVerifiedActor
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // The requirement is keyed on the ambient tenant (set from the same JWT as ActorUserId), so
        // it holds even if the actor row can't be read.
        var required = await db.Tenants
            .AnyAsync(t => t.Id == tenant.TenantId && t.RequireTwoFactor, cancellationToken);
        if (required)
        {
            var enrolled = await db.Users
                .AnyAsync(u => u.Id == request.ActorUserId && u.TwoFactorEnabled, cancellationToken);
            if (!enrolled)
            {
                throw new TwoFactorRequiredException();
            }
        }

        return await next();
    }
}
