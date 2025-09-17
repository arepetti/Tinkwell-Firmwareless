namespace Tinkwell.Firmwareless.WasmHost.Runtime;

interface IPublicRepository
{
    Task<string> GetPublicKeyAsync(CancellationToken cancellationToken);

    Task<string> DownloadFirmwareAsync(ProductEntry product, CancellationToken cancellationToken);
}