namespace Tinkwell.Firmwareless.WasmHost.Packages;

interface IPackageValidator
{
    FirmwarelessHostInformation HostInfo { get; set; }

    Task ValidateAsync(string path, CancellationToken cancellationToken);
}
