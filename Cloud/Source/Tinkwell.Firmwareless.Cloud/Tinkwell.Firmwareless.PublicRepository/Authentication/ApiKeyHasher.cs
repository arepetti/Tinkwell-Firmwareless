using System.Security.Cryptography;
using System.Text;

namespace Tinkwell.Firmwareless.PublicRepository.Authentication;

static class ApiKeyHasher
{
    public static string NewKeyBytes(int bytes = 32)
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes));

    public static (string Hash, string Salt) Hash(string key)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        var salt = Convert.ToBase64String(saltBytes);
        var hash = HashWithSalt(key, salt);
        return (hash, salt);
    }

    public static string HashWithSalt(string key, string saltBase64)
    {
        var saltBytes = Convert.FromBase64String(saltBase64);
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var toHash = new byte[keyBytes.Length + saltBytes.Length];
        Buffer.BlockCopy(keyBytes, 0, toHash, 0, keyBytes.Length);
        Buffer.BlockCopy(saltBytes, 0, toHash, keyBytes.Length, saltBytes.Length);
        var digest = SHA256.HashData(toHash);
        return Convert.ToBase64String(digest);
    }

    public static bool FixedTimeEquals(string base64A, string base64B)
        => CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(base64A), Convert.FromBase64String(base64B));
}
