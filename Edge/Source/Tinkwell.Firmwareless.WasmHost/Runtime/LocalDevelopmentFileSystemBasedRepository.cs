using System.Text;

namespace Tinkwell.Firmwareless.WasmHost.Runtime;

sealed class LocalDevelopmentFileSystemBasedRepository : IPublicRepository
{
    public Task<string> GetPublicKeyAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(BaseDirectory, "public_key.pem");
        if (!File.Exists(path))
            throw new FileNotFoundException("Public key file not found", path);

        return Task.FromResult(File.ReadAllText(path, Encoding.UTF8));
    }

    public Task<string> DownloadFirmwareAsync(ProductEntry product, CancellationToken cancellationToken)
    {
        var source = Path.Combine(BaseDirectory,
            product.VendorId,
            product.ProductId,
            product.FirmwareVersion ?? "latest",
            "firmlet.zip");

        var destinationPath = Path.Combine(AppLocations.FirmletsPackagesPath, $"firmlet-{product.ProductId}.zip");
        File.Copy(source, destinationPath);
        return Task.FromResult(destinationPath);
    }

    private static string BaseDirectory
        => Path.Combine(AppContext.BaseDirectory, "LocalRepository");
}
