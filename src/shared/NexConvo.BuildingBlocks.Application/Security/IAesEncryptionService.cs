namespace NexConvo.BuildingBlocks.Application.Security;

public interface IAesEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}
