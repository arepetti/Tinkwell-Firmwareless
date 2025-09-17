namespace Tinkwell.Firmwareless.WasmHost.Packages;

interface IFirmletsManager
{
    Task StartAsync(CancellationToken cancellationToken);
}
