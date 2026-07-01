namespace NexConvo.BuildingBlocks.Domain;

/// <summary>
/// Thrown when a user in a workspace that requires two-factor authentication attempts a sensitive
/// write without having 2FA enabled. Caught at the API edge and mapped to 403 (soft enforcement).
/// </summary>
public sealed class TwoFactorRequiredException()
    : Exception("Enable two-factor authentication to perform this action.");
