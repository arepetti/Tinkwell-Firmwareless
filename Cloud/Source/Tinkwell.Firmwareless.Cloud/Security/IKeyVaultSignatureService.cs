namespace Tinkwell.Firmwareless.Cloud.Security;

public interface IKeyVaultSignatureService
{
    Task<string> GetPublicKeyAsync(CancellationToken cancellationToken);
    Task<byte[]> SignDataAsync(string data, CancellationToken cancellationToken);
}
