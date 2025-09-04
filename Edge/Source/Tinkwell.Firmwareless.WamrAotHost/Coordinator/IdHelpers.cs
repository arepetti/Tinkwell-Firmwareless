using System.Security.Cryptography;
using System.Text;

namespace Tinkwell.Firmwareless.WamrAotHost.Coordinator;

static class IdHelpers
{
    public static string CreateId(string prefix, int length = 12)
        => DeriveId(prefix, Guid.NewGuid().ToString("N"), length);

    public static string DeriveId(string prefix, string id, int length = 12)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(id));
        return $"{prefix}_{Convert.ToHexStringLower(hash)[..length]}";
    }
}