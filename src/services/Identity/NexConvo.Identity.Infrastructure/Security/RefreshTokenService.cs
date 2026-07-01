using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using NexConvo.Identity.Application.Abstractions;

namespace NexConvo.Identity.Infrastructure.Security;

/// <summary>
/// Generates opaque refresh tokens of the form <c>{base64url(tenantId)}.{base64url(random)}</c>,
/// so the refresh endpoint can recover the tenant (for RLS) from the token alone. Only the
/// SHA-256 hash is ever stored.
/// </summary>
public sealed class RefreshTokenService(IConfiguration configuration, IClock clock) : IRefreshTokenService
{
    public GeneratedRefreshToken Generate(Guid tenantId)
    {
        var random = RandomNumberGenerator.GetBytes(32);
        var token = $"{Base64UrlEncoder.Encode(tenantId.ToByteArray())}.{Base64UrlEncoder.Encode(random)}";
        var days = configuration.GetValue("Jwt:RefreshTokenDays", 14);
        return new GeneratedRefreshToken(token, Hash(token), clock.UtcNow.AddDays(days));
    }

    public string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public bool TryGetTenantId(string token, out Guid tenantId)
    {
        tenantId = Guid.Empty;
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        var dot = token.IndexOf('.', StringComparison.Ordinal);
        if (dot <= 0)
        {
            return false;
        }

        try
        {
            var bytes = Base64UrlEncoder.DecodeBytes(token[..dot]);
            if (bytes.Length != 16)
            {
                return false;
            }

            tenantId = new Guid(bytes);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
