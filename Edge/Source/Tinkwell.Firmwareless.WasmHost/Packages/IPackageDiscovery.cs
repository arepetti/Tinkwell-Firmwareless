using Tinkwell.Firmwareless.WasmHost.Runtime;

namespace Tinkwell.Firmwareless.WasmHost.Packages;

interface IPackageDiscovery
{
    Task<IEnumerable<ProductEntry>> DiscoverAsync(CancellationToken cancellationToken);
}
