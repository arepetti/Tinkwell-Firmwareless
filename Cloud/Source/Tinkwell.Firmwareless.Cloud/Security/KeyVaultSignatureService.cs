using Azure.Security.KeyVault.Keys.Cryptography;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace Tinkwell.Firmwareless.Cloud.Security;

public sealed class KeyVaultSignatureService : IKeyVaultSignatureService
{
    public KeyVaultSignatureService(CryptographyClient cryptoClient, ILogger<KeyVaultSignatureService> logger)
    {
        _cryptoClient = cryptoClient;
        _logger = logger;
    }

    public Task<string> GetPublicKeyAsync(CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public async Task<byte[]> SignDataAsync(string data, CancellationToken cancellationToken)
    {
        var finalDigest = SHA512.HashData(Encoding.UTF8.GetBytes(data));

        _logger.LogInformation("Sending hash to Key Vault for signing with key: {KeyId}", _cryptoClient.KeyId);
        SignResult result = await _cryptoClient.SignAsync(SignatureAlgorithm.RS512, finalDigest, cancellationToken);
        _logger.LogDebug("Successfully received signature from Key Vault.");

        return result.Signature;
    }

    private readonly CryptographyClient _cryptoClient;
    private readonly ILogger<KeyVaultSignatureService> _logger;
}
