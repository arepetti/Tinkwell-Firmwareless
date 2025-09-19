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

    public async Task<string> DownloadFirmwareAsync(ProductEntry product, CancellationToken cancellationToken)
    {   
        var version = await GetLatestFirmletVersionAsync(product, cancellationToken);
        var source = Path.Combine(BaseDirectory,
            product.VendorId,
            product.ProductId,
            version,
            "firmlet.zip");

        var destinationPath = GetPathFor(product);
        File.Copy(source, destinationPath);
        product.FirmwareVersion = version;
        return destinationPath;
    }

    public Task<string> GetLatestFirmletVersionAsync(ProductEntry product, CancellationToken cancellationToken)
    {
        var path = Path.Combine(BaseDirectory, product.VendorId, product.ProductId);
        return Task.FromResult(Directory.EnumerateDirectories(path)
            .Select(x => new Version(Path.GetFileName(x)))
            .OrderByDescending(x => x)
            .First()
            .ToString());
    }

    private static string BaseDirectory
        => Path.Combine(AppContext.BaseDirectory, "LocalRepository");

    private static string GetPathFor(ProductEntry product)
        => Path.Combine(AppLocations.FirmletsPackagesPath, $"{product.Type.ToString().ToLowerInvariant()}-{product.ProductId}.zip");
}
