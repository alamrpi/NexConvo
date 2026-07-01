namespace NexConvo.Identity.Application.Common;

/// <summary>
/// Marks a command whose acting user must have a verified email (soft enforcement). The
/// <see cref="VerifiedActorBehavior{TRequest,TResponse}"/> checks this before the handler runs.
/// </summary>
public interface IRequireVerifiedActor
{
    /// <summary>The id of the user performing the action (from the validated JWT).</summary>
    Guid ActorUserId { get; }
}
