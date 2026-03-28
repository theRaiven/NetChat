using System.Security.Cryptography;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace NetChat;
public static class Crypto
{
    static readonly byte[] Key = Encoding.UTF8.GetBytes("1234567890123456");

    static readonly byte[] IV = Encoding.UTF8.GetBytes("6543210987654321");
    public static string Encrypt(string text)
    {
        using Aes aes = Aes.Create();
        aes.Key = Key;
        aes.IV = IV;

        using var encryptor = aes.CreateEncryptor();

        byte[] bytes = Encoding.UTF8.GetBytes(text);
        byte[] encrypted = encryptor.TransformFinalBlock(bytes, 0, bytes.Length);

        return Convert.ToBase64String(encrypted);
    }
    public static string Decrypt(string cipher)
    {
        using Aes aes = Aes.Create();
        aes.Key = Key;
        aes.IV = IV;

        using var decryptor = aes.CreateDecryptor();

        byte[] bytes = Convert.FromBase64String(cipher);
        byte[] decrypted = decryptor.TransformFinalBlock(bytes, 0, bytes.Length);

        return Encoding.UTF8.GetString(decrypted);
    }
}
