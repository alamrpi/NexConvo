using NexConvo.Identity.Domain.Tenants;
using NexConvo.Identity.Domain.Users;

namespace NexConvo.Identity.Application.Abstractions;

/// <summary>Hashes and verifies passwords (PBKDF2). Never stores or logs the plaintext.</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string passwordHash, string password);
}

/// <summary>A signed access token plus its lifetime.</summary>
public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt, int ExpiresInSeconds);

/// <summary>Issues RS256 access tokens carrying tenant + role + permission claims.</summary>
public interface IJwtTokenIssuer
{
    AccessToken Issue(
        User user,
        Tenant tenant,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> permissions);
}

/// <summary>A freshly generated opaque refresh token: the value (given to the client) + its stored hash.</summary>
public sealed record GeneratedRefreshToken(string Token, string TokenHash, DateTimeOffset ExpiresAt);

/// <summary>
/// Creates and hashes opaque refresh tokens. The token embeds its tenant id so the refresh
/// endpoint can set tenant context (for RLS) without an extra slug parameter.
/// </summary>
public interface IRefreshTokenService
{
    GeneratedRefreshToken Generate(Guid tenantId);
    string Hash(string token);
    bool TryGetTenantId(string token, out Guid tenantId);
}

/// <summary>Lets auth handlers set the tenant on the connection before RLS-scoped queries.</summary>
public interface IAmbientTenantSetter
{
    void SetTenant(Guid tenantId);
}

/// <summary>Abstracts the clock for deterministic tests.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>Writes a tenant-scoped audit entry (skill Standard 14). Identifiers only — never PII/passwords.</summary>
public interface IAuditWriter
{
    void Add(string action, Guid tenantId, Guid? userId, string? detail);
}
