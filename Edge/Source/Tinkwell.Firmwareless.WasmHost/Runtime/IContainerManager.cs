namespace Tinkwell.Firmwareless.WasmHost.Runtime;

interface IContainerManager
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
