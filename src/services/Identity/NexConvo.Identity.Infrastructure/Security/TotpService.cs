using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using NexConvo.Identity.Application.Abstractions;

namespace NexConvo.Identity.Infrastructure.Security;

/// <summary>
/// Hand-rolled RFC 6238 TOTP (RFC 4226 HOTP over HMAC-SHA1) using only <see cref="System.Security.Cryptography"/>
/// — no third-party OTP package, so the algorithm/parameters stay fully under our control.
/// </summary>
public sealed class TotpService(IClock clock) : ITotpService
{
    private const int Digits = 6;
    private const int PeriodSeconds = 30;
    private const int SecretBytes = 20; // 160-bit, per RFC 6238 recommendation for SHA-1
    private const string Issuer = "NexConvo";

    public string GenerateSecret() => Base32.Encode(RandomNumberGenerator.GetBytes(SecretBytes));

    public string BuildOtpAuthUri(string secretBase32, string accountName)
    {
        var label = Uri.EscapeDataString($"{Issuer}:{accountName}");
        var issuer = Uri.EscapeDataString(Issuer);
        return $"otpauth://totp/{label}?secret={secretBase32}&issuer={issuer}&algorithm=SHA1&digits={Digits}&period={PeriodSeconds}";
    }

    public bool Verify(string secretBase32, string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != Digits)
        {
            return false;
        }

        byte[] key;
        try
        {
            key = Base32.Decode(secretBase32);
        }
        catch (FormatException)
        {
            return false;
        }

        var step = (long)(clock.UtcNow.ToUnixTimeSeconds() / PeriodSeconds);
        for (var drift = -1; drift <= 1; drift++) // tolerate ±1 step of clock skew
        {
            if (FixedTimeEquals(ComputeCode(key, step + drift), code))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>RFC 4226 HOTP: HMAC-SHA1 of the counter, then dynamic truncation to N digits.</summary>
    [SuppressMessage("Security", "CA5350",
        Justification = "RFC 6238 TOTP mandates HMAC-SHA1; required for authenticator-app interoperability.")]
    internal static string ComputeCode(byte[] key, long counter)
    {
        Span<byte> counterBytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counterBytes, counter);

        var hash = HMACSHA1.HashData(key, counterBytes);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
                     | ((hash[offset + 1] & 0xFF) << 16)
                     | ((hash[offset + 2] & 0xFF) << 8)
                     | (hash[offset + 3] & 0xFF);

        var otp = binary % (int)Math.Pow(10, Digits);
        return otp.ToString().PadLeft(Digits, '0');
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(a), Encoding.ASCII.GetBytes(b));
}
