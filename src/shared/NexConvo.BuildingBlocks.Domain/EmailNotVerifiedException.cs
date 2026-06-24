namespace NexConvo.BuildingBlocks.Domain;

/// <summary>
/// Thrown when an unverified user attempts a sensitive write that requires a verified email.
/// Caught at the API edge and mapped to 403 (soft email-verification enforcement).
/// </summary>
public sealed class EmailNotVerifiedException()
    : Exception("Verify your email address to perform this action.");
