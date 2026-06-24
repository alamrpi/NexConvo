using System.Security.Cryptography;
using System.Text;

namespace NexConvo.Identity.Application.Account.TwoFactor;

/// <summary>
/// Generates and hashes 2FA recovery codes. Plaintext is shown once; only SHA-256 hashes persist.
/// Hand-rolled (no package) — a tiny RNG + hash over a non-ambiguous alphabet.
/// </summary>
internal static class BackupCodes
{
    // 32 chars, excluding ambiguous 0/O/1/I — length 32 divides 256 evenly (no modulo bias).
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 10;

    public static (IReadOnlyList<string> Plaintext, IReadOnlyList<string> Hashes) Generate(int count)
    {
        var plaintext = new List<string>(count);
        var hashes = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            var code = RandomCode();
            plaintext.Add($"{code[..5]}-{code[5..]}"); // grouped for readability
            hashes.Add(Hash(code));
        }

        return (plaintext, hashes);
    }

    /// <summary>Hashes a (possibly user-entered, dashed) code for comparison against stored hashes.</summary>
    public static string Hash(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(code))));

    private static string Normalize(string code) =>
        code.Replace("-", string.Empty).Trim().ToUpperInvariant();

    private static string RandomCode()
    {
        Span<byte> bytes = stackalloc byte[CodeLength];
        RandomNumberGenerator.Fill(bytes);
        var chars = new char[CodeLength];
        for (var i = 0; i < CodeLength; i++)
        {
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        }

        return new string(chars);
    }
}
