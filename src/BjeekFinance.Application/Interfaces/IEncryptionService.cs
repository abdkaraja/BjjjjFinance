namespace BjeekFinance.Application.Interfaces;

public interface IEncryptionService
{
    string Encrypt(string plainText, string? context = null);
    string Decrypt(string cipherText, string? context = null);
}
