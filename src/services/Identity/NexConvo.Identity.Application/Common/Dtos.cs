namespace NexConvo.Identity.Application.Common;

/// <summary>Tokens returned by signup/login/refresh.</summary>
public sealed record AuthTokensDto(string AccessToken, int ExpiresInSeconds, string RefreshToken);

/// <summary>Login outcome: either issued <see cref="Tokens"/>, or a 2FA challenge to complete.</summary>
public sealed record LoginResultDto(AuthTokensDto? Tokens, bool TwoFactorRequired, string? ChallengeToken);

/// <summary>The authenticated user's profile for GET /me.</summary>
public sealed record CurrentUserDto(
    Guid UserId,
    Guid TenantId,
    string TenantSlug,
    string Email,
    string FullName,
    bool EmailVerified,
    bool TwoFactorEnabled,
    bool WorkspaceRequiresTwoFactor,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);
