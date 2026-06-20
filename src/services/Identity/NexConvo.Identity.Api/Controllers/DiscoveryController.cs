using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NexConvo.Identity.Infrastructure.Security;

namespace NexConvo.Identity.Api.Controllers;

/// <summary>
/// OIDC discovery + JWKS so the gateway and other services can validate Identity-issued tokens
/// via <c>Jwt:Authority</c> without sharing a secret (skill: asymmetric signing).
/// </summary>
[ApiController]
[AllowAnonymous]
public sealed class DiscoveryController(RsaKeyProvider keys, IConfiguration configuration) : ControllerBase
{
    [HttpGet("/.well-known/openid-configuration")]
    public IActionResult OpenIdConfiguration()
    {
        var issuer = configuration["Jwt:Issuer"];
        return Ok(new
        {
            issuer,
            jwks_uri = $"{issuer}/.well-known/jwks.json",
            id_token_signing_alg_values_supported = new[] { "RS256" },
            response_types_supported = new[] { "token" },
            subject_types_supported = new[] { "public" },
        });
    }

    [HttpGet("/.well-known/jwks.json")]
    public IActionResult Jwks()
    {
        var jwk = keys.GetPublicJsonWebKey();
        return Ok(new
        {
            keys = new[]
            {
                new { kty = jwk.Kty, use = jwk.Use, kid = jwk.Kid, alg = jwk.Alg, n = jwk.N, e = jwk.E },
            },
        });
    }
}
