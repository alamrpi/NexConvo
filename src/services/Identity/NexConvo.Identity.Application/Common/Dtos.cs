namespace NexConvo.Identity.Application.Common;

/// <summary>Tokens returned by signup/login/refresh.</summary>
public sealed record AuthTokensDto(string AccessToken, int ExpiresInSeconds, string RefreshToken);

/// <summary>The authenticated user's profile for GET /me.</summary>
public sealed record CurrentUserDto(
    Guid UserId,
    Guid TenantId,
    string TenantSlug,
    string Email,
    string FullName,
    bool EmailVerified,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);
