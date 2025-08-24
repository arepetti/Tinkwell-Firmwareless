using System.Security.Cryptography;
using System.Text;

namespace Tinkwell.Firmwareless.CompilationServer.Services;

public sealed class LocalDevelopmentSignatureService : IKeyVaultSignatureService
{
    public Task<byte[]> SignDataAsync(string data, CancellationToken cancellationToken)
        => Task.FromResult(SHA512.HashData(Encoding.UTF8.GetBytes(data)));
}
