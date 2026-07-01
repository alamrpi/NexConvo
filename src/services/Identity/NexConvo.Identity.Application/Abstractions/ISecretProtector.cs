namespace NexConvo.Identity.Application.Abstractions;

/// <summary>
/// Encrypts/decrypts secrets at rest (per-workspace SMTP password / Resend API key).
/// Plaintext secrets never touch the database or logs (skill Standard 13/15).
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}
