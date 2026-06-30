using Microsoft.Extensions.Configuration;
using NexConvo.BuildingBlocks.Application.Security;
using System.Security.Cryptography;
using System.Text;

namespace NexConvo.BuildingBlocks.Infrastructure.Security;

public class AesEncryptionService : IAesEncryptionService
{
    private readonly byte[] _key;

    public AesEncryptionService(IConfiguration configuration)
    {
        var masterKey = configuration["SecuritySettings:AiMasterKey"];
        if (string.IsNullOrEmpty(masterKey) || masterKey.Length < 32)
        {
            throw new ArgumentException("SecuritySettings:AiMasterKey must be at least 32 characters long in configuration.");
        }
        
        // Take exactly 32 bytes for AES-256
        _key = Encoding.UTF8.GetBytes(masterKey)[..32];
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;

        using var aesAlg = Aes.Create();
        aesAlg.Key = _key;
        aesAlg.GenerateIV(); // Random IV for each encryption

        using var encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
        using var msEncrypt = new MemoryStream();
        
        // Write the IV at the beginning of the stream so we can extract it during decryption
        msEncrypt.Write(aesAlg.IV, 0, aesAlg.IV.Length);

        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
        using (var swEncrypt = new StreamWriter(csEncrypt))
        {
            swEncrypt.Write(plainText);
        }

        return Convert.ToBase64String(msEncrypt.ToArray());
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;

        var fullCipher = Convert.FromBase64String(cipherText);

        using var aesAlg = Aes.Create();
        aesAlg.Key = _key;

        // Extract the IV from the first 16 bytes
        var iv = new byte[aesAlg.BlockSize / 8];
        var cipherBytes = new byte[fullCipher.Length - iv.Length];

        Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
        Buffer.BlockCopy(fullCipher, iv.Length, cipherBytes, 0, cipherBytes.Length);

        aesAlg.IV = iv;

        using var decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
        using var msDecrypt = new MemoryStream(cipherBytes);
        using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using var srDecrypt = new StreamReader(csDecrypt);

        return srDecrypt.ReadToEnd();
    }
}
