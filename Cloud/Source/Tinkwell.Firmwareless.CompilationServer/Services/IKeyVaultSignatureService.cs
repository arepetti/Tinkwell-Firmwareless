namespace Tinkwell.Firmwareless.CompilationServer.Services;

public interface IKeyVaultSignatureService
{
    Task<byte[]> SignDataAsync(string data, CancellationToken cancellationToken);
}
