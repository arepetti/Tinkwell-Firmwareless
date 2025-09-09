namespace Tinkwell.Firmwareless.WasmHost;

interface IContainerManager
{
    Task StartAsync(string baseDirectory, CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
