using System.Security.Cryptography;
using System.Text;

namespace Tinkwell.Firmwareless.WamrAotHost.Coordinator;

static class IdHelpers
{
    public static string CreateId(string prefix, int length = 12)
    {
        string id = Guid.NewGuid().ToString("N");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(id));
        return $"{prefix}_{Convert.ToHexStringLower(hash)[..length]}";
    }
}