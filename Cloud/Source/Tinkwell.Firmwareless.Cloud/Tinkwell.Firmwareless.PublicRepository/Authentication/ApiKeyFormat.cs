using System.Security.Cryptography;
using System.Text;

namespace Tinkwell.Firmwareless.PublicRepository.Authentication;

static class ApiKeyFormat
{
    public static string Generate(Guid keyId, ApiKeyOptions opts)
    {
        var idBytes = keyId.ToByteArray(); // 16 bytes
        var hmac = Hmac(opts.HmacSecret, idBytes);
        var payload = new byte[idBytes.Length + opts.HmacBytes];
        Buffer.BlockCopy(idBytes, 0, payload, 0, idBytes.Length);
        Buffer.BlockCopy(hmac, 0, payload, idBytes.Length, opts.HmacBytes);
        return opts.KeyPrefix + Base64Url.Encode(payload);
    }

    public static bool TryParseAndValidate(string presented, ApiKeyOptions opts, out Guid keyId)
    {
        keyId = default;

        if (string.IsNullOrWhiteSpace(presented) || !presented.StartsWith(opts.KeyPrefix))
            return false;

        var token = presented[opts.KeyPrefix.Length..];
        if (!TryDecodeBase64(token, out var payload))
            return false;

        if (payload.Length < 16 + opts.HmacBytes)
            return false;

        var idBytes = payload.AsSpan(0, 16).ToArray();
        var hmacBytes = payload.AsSpan(16, opts.HmacBytes).ToArray();

        var expected = Hmac(opts.HmacSecret, idBytes);
        if (CryptographicOperations.FixedTimeEquals(hmacBytes, expected.AsSpan(0, opts.HmacBytes).ToArray()) == false)
            return false;

        keyId = new Guid(idBytes);
        return true;
    }

    private static byte[] Hmac(string secret, byte[] data)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return h.ComputeHash(data);
    }

    private static bool TryDecodeBase64(string input, out byte[] result)
    {
        try
        {
            result = Base64Url.Decode(input);
            return true;
        }
        catch
        {
            result = [];
            return false;
        }
    }
}
