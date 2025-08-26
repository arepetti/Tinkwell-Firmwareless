using System.Security.Cryptography;
using System.Text;

namespace Tinkwell.Firmwareless.Cloud.Security;

public sealed class LocalDevelopmentSignatureService : IKeyVaultSignatureService
{
    public Task<string> GetPublicKeyAsync(CancellationToken cancellationToken)
        => Task.FromResult(Properties.Resources.development_only_public_key);

    public Task<byte[]> SignDataAsync(string data, CancellationToken cancellationToken)
    {
        var finalDigest = SHA512.HashData(Encoding.UTF8.GetBytes(data));

        using RSA rsa = RSA.Create();
        rsa.ImportFromPem(Properties.Resources.development_only_private_key);

        return Task.FromResult(rsa.SignData(finalDigest, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1));
    }
}
