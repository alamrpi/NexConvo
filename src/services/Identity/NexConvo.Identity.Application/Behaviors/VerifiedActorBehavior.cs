using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NexConvo.BuildingBlocks.Domain;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Application.Common;

namespace NexConvo.Identity.Application.Behaviors;

/// <summary>
/// Blocks <see cref="IRequireVerifiedActor"/> commands when the acting user hasn't verified their
/// email (soft enforcement, toggled by <see cref="AuthOptions.RequireVerifiedEmailForWrites"/>).
/// Checks live DB state — not the JWT — so verifying takes effect immediately. Throws
/// <see cref="EmailNotVerifiedException"/>, mapped to 403 at the API edge.
/// </summary>
public sealed class VerifiedActorBehavior<TRequest, TResponse>(
    IIdentityDbContext db,
    IOptions<AuthOptions> authOptions) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequireVerifiedActor
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (authOptions.Value.RequireVerifiedEmailForWrites)
        {
            var verified = await db.Users
                .AnyAsync(u => u.Id == request.ActorUserId && u.EmailVerifiedAt != null, cancellationToken);
            if (!verified)
            {
                throw new EmailNotVerifiedException();
            }
        }

        return await next();
    }
}
