using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NexConvo.Identity.Application.Abstractions;
using NexConvo.Identity.Domain.Tenants;
using NexConvo.Identity.Domain.Users;

namespace NexConvo.Identity.Infrastructure.Security;

/// <summary>Issues RS256 access tokens (skill: asymmetric signing) with tenant/role/permission claims.</summary>
public sealed class JwtTokenIssuer(RsaKeyProvider keys, IConfiguration configuration, IClock clock) : IJwtTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken Issue(
        User user,
        Tenant tenant,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> permissions)
    {
        var issuer = configuration["Jwt:Issuer"]
                     ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
        var audience = configuration["Jwt:Audience"] ?? "nexconvo-api";
        var minutes = configuration.GetValue("Jwt:AccessTokenMinutes", 15);

        var now = clock.UtcNow;
        var expires = now.AddMinutes(minutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(keys.SecurityKey, SecurityAlgorithms.RsaSha256),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = user.Id.ToString(),
                ["tenant_id"] = tenant.Id.ToString(),
                ["tenant_slug"] = tenant.Slug.Value,
                ["email"] = user.Email.Value,
                ["name"] = user.FullName,
                ["role"] = roles.ToArray(),
                ["permission"] = permissions.ToArray(),
                ["jti"] = Guid.NewGuid().ToString(),
            },
        };

        var token = _handler.CreateToken(descriptor);
        return new AccessToken(token, expires, (int)(expires - now).TotalSeconds);
    }
}
