using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace NexConvo.Identity.Infrastructure.Security;

/// <summary>
/// Holds the RSA signing key. In non-Development the private key PEM is required from config
/// (<c>Jwt:SigningKey:PrivateKeyPem</c> — sourced from env/Key Vault, never committed, skill
/// Standard 13). In Development an ephemeral key is generated and a warning logged.
/// The <see cref="KeyId"/> is a stable thumbprint so issued tokens match the published JWKS.
/// </summary>
public sealed class RsaKeyProvider : IDisposable
{
    private readonly RSA _rsa;

    public RsaSecurityKey SecurityKey { get; }
    public string KeyId { get; }

    public RsaKeyProvider(IConfiguration configuration, IHostEnvironment environment, ILogger<RsaKeyProvider> logger)
    {
        var pem = configuration["Jwt:SigningKey:PrivateKeyPem"];

        if (!string.IsNullOrWhiteSpace(pem))
        {
            _rsa = RSA.Create();
            _rsa.ImportFromPem(pem);
        }
        else if (environment.IsDevelopment())
        {
            _rsa = RSA.Create(2048);
            logger.LogWarning(
                "No Jwt:SigningKey configured — generated an EPHEMERAL development RSA key. " +
                "Issued tokens will not survive a restart. Configure a persistent key for any shared environment.");
        }
        else
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey:PrivateKeyPem is required outside Development.");
        }

        KeyId = ComputeKeyId(_rsa);
        SecurityKey = new RsaSecurityKey(_rsa) { KeyId = KeyId };
    }

    /// <summary>The public key as a JWK (used by the JWKS endpoint). Never includes private parameters.</summary>
    public JsonWebKey GetPublicJsonWebKey()
    {
        using var publicOnly = RSA.Create();
        publicOnly.ImportParameters(_rsa.ExportParameters(includePrivateParameters: false));

        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(publicOnly));
        jwk.KeyId = KeyId;
        jwk.Use = "sig";
        jwk.Alg = SecurityAlgorithms.RsaSha256;
        return jwk;
    }

    private static string ComputeKeyId(RSA rsa)
    {
        var parameters = rsa.ExportParameters(includePrivateParameters: false);
        var material = (parameters.Modulus ?? []).Concat(parameters.Exponent ?? []).ToArray();
        return Base64UrlEncoder.Encode(SHA256.HashData(material));
    }

    public void Dispose() => _rsa.Dispose();
}
