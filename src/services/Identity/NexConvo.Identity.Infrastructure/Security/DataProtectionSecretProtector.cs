using Microsoft.AspNetCore.DataProtection;
using NexConvo.Identity.Application.Abstractions;

namespace NexConvo.Identity.Infrastructure.Security;

/// <summary>Encrypts workspace email secrets at rest via ASP.NET Core Data Protection.</summary>
public sealed class DataProtectionSecretProtector : ISecretProtector
{
    private readonly IDataProtector _protector;

    public DataProtectionSecretProtector(IDataProtectionProvider provider) =>
        _protector = provider.CreateProtector("workspace-email-secret");

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);
}
