using Microsoft.AspNetCore.Identity;
using NexConvo.Identity.Application.Abstractions;

namespace NexConvo.Identity.Infrastructure.Security;

/// <summary>PBKDF2 password hashing via Microsoft's <see cref="PasswordHasher{TUser}"/> (no full Identity framework).</summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private static readonly object Subject = new();
    private readonly PasswordHasher<object> _inner = new();

    public string Hash(string password) => _inner.HashPassword(Subject, password);

    public bool Verify(string passwordHash, string password) =>
        _inner.VerifyHashedPassword(Subject, passwordHash, password) != PasswordVerificationResult.Failed;
}
