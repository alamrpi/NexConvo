namespace NexConvo.Identity.Application.Common;

/// <summary>
/// Tunable auth-security policy (bound from the <c>Auth</c> configuration section). Defaults are
/// safe for production; tests may shrink the grace window to exercise reuse-detection.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Consecutive failed logins before an account is locked.</summary>
    public int LockoutMaxAttempts { get; set; } = 5;

    /// <summary>How long an account stays locked after the threshold is hit.</summary>
    public int LockoutWindowMinutes { get; set; } = 15;

    /// <summary>Window in which a just-rotated refresh token may be reused without penalty
    /// (tolerates benign client races). Reuse after this window is treated as token theft.</summary>
    public int RefreshReuseGraceSeconds { get; set; } = 60;
}
