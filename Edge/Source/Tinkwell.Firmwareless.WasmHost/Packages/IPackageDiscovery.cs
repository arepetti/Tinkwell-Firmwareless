namespace Tinkwell.Firmwareless.WasmHost.Packages;

interface IPackageDiscovery
{
    Task<IEnumerable<string>> DiscoverAsync(string baseDirectory, CancellationToken cancellationToken);
}
