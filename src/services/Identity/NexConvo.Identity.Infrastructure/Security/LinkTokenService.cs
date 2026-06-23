using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using NexConvo.Identity.Application.Abstractions;

namespace NexConvo.Identity.Infrastructure.Security;

/// <summary>
/// Opaque one-time link tokens of the form <c>{base64url(tenantId)}.{base64url(random)}</c>,
/// so verify/reset/accept endpoints can recover the tenant (for RLS) from the token alone.
/// Only the SHA-256 hash is stored.
/// </summary>
public sealed class LinkTokenService : ILinkTokenService
{
    public GeneratedLinkToken Create(Guid tenantId)
    {
        var random = RandomNumberGenerator.GetBytes(32);
        var token = $"{Base64UrlEncoder.Encode(tenantId.ToByteArray())}.{Base64UrlEncoder.Encode(random)}";
        return new GeneratedLinkToken(token, Hash(token));
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
