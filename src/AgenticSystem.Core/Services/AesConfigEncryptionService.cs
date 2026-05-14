using System.Security.Cryptography;
using System.Text;
using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Encriptação AES-256 para valores sensíveis de configuração.
/// Em produção, a chave deve vir de um Key Vault (Azure/AWS/HashiCorp).
/// </summary>
public class AesConfigEncryptionService : IConfigEncryptionService
{
    private readonly byte[] _key;

    public AesConfigEncryptionService(string? encryptionKey = null)
    {
        if (!string.IsNullOrEmpty(encryptionKey))
        {
            _key = SHA256.HashData(Encoding.UTF8.GetBytes(encryptionKey));
        }
        else
        {
            // Fallback: chave estática fixa para desenvolvimento (evita quebra ao recriar container Docker)
            _key = SHA256.HashData(Encoding.UTF8.GetBytes("AgenticSystem_Development_StaticSecretKey_Fallback"));
        }
    }

    public string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string cipherText)
    {
        try
        {
            var fullCipher = Convert.FromBase64String(cipherText);

            using var aes = Aes.Create();
            aes.Key = _key;

            var iv = new byte[aes.BlockSize / 8];
            var cipher = new byte[fullCipher.Length - iv.Length];

            Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);

            aes.IV = iv;
            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (FormatException)
        {
            return string.Empty;
        }
        catch (CryptographicException)
        {
            return string.Empty;
        }
    }

    public string Hash(string value)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToBase64String(hashBytes);
    }
}
