namespace NexConvo.Identity.Application.Abstractions;

/// <summary>A freshly generated link token: the plaintext (goes in the emailed link) + its stored hash.</summary>
public sealed record GeneratedLinkToken(string Token, string TokenHash);

/// <summary>
/// Creates opaque, tenant-embedded one-time tokens for emailed links (verify / reset / invite),
/// so the unauthenticated endpoint can recover the tenant (for RLS) from the token alone.
/// </summary>
public interface ILinkTokenService
{
    GeneratedLinkToken Create(Guid tenantId);
    string Hash(string token);
    bool TryGetTenantId(string token, out Guid tenantId);
}

/// <summary>Builds absolute front-end URLs for emailed links (base URL from config).</summary>
public interface IAppLinkBuilder
{
    string VerifyEmailLink(string token);
    string ResetPasswordLink(string token);
    string AcceptInvitationLink(string token);
}
