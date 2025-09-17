using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Tinkwell.Firmwareless.WasmHost.Packages;

sealed class FirmletsManager(ILogger<HostedService> logger, IPackageDiscovery discovery) : IFirmletsManager
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        // Search for packaged firmlets
        var products = await _discovery.DiscoverAsync(cancellationToken);

        // Unpack packages into a "cache" directory accessible to the Docker container
        foreach (var product in products.Where(x => !x.Disabled))
        {
            _logger.LogDebug("Discovered valid firmlet: {Path}", Path.GetFileName(product.Package));
            var firmletEntry = UnpackFirmwarePackage(product.Package!);
            if (firmletEntry is not null)
                _firmlets.Add(firmletEntry);
        }

        await WriteFirmwareListAsync(cancellationToken);

        // Delete orphans (firmlets that are unpacked but not referenced by any package)
        int deletedFirmletsCount = DeleteOrphanedFirmlets();

        stopwatch.Stop();
        _logger.LogDebug("In use {Count} firmlets, {Deleted} orphans. {ElapsedMilliseconds} ms",
            _firmlets.Count,
            deletedFirmletsCount,
            stopwatch.ElapsedMilliseconds);
    }

    private sealed record FirmwareEntry(string Path, PackageManifest Manifest);

    private readonly ILogger<HostedService> _logger = logger;
    private readonly IPackageDiscovery _discovery = discovery;
    private readonly List<FirmwareEntry> _firmlets = new();

    private FirmwareEntry? UnpackFirmwarePackage(string path)
    {
        try
        {
            var manifest = PackageManifestReader.Read(path);

            // If we change this then we must update FindUnpackagedFirmlets() as well.
            var firmwareDirectoryName = Path.Combine(
                AppLocations.FirmletsPath,
                ShortenGuid(manifest.VendorId, 12),
                ShortenGuid(manifest.ProductId, 8),
                Sanitize(manifest.FirmwareVersion));

            if (Directory.Exists(firmwareDirectoryName))
                return new(firmwareDirectoryName, manifest);

            _logger.LogInformation("Unpacking {Name} to {Path}", Path.GetFileName(path), firmwareDirectoryName);
            PackageUnpacker.ExtractToDirectory(path, firmwareDirectoryName);
            return new(firmwareDirectoryName, manifest);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Cannot unpack firmware {Name} to {Path}", Path.GetFileName(path), AppLocations.FirmletsPath);
        }

        return null;

        static string ShortenGuid(string id, int length)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(id));
            return Convert.ToHexStringLower(hash)[..length];
        }

        static string Sanitize(string text)
            => text.Replace('/', '_').Replace('\\', '_'); // A bit paranoic but it's an "external" input...
    }

    private int DeleteOrphanedFirmlets()
    {
        // Note that this orphans are not the same deleted in PackageDiscovery! Here we delete orphan unpacked firmlets (directories)
        // while in PackageDiscovery we delete the firmlet packages (.zip).
        var allFirmletsPaths = FindUnpackagedFirmlets();
        var firmletsInUsePaths = _firmlets.Select(x => x.Path);
        var orphanedFirmletsPaths = allFirmletsPaths
            .Except(firmletsInUsePaths)
            .ToArray();

        if (orphanedFirmletsPaths.Length == 0)
            return 0;

        _logger.LogInformation("Deleting {Count} orphaned firmlets", orphanedFirmletsPaths.Length);
        foreach (var orphanedFirmletPath in orphanedFirmletsPaths)
        {
            try
            {
                Directory.Delete(orphanedFirmletPath, true);
                _logger.LogDebug("Deleted orphaned firmlet at {Path}", orphanedFirmletPath);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Cannot delete orphaned firmlet at {Path}", orphanedFirmletPath);
            }
        }

        return orphanedFirmletsPaths.Length;
    }

    private static List<string> FindUnpackagedFirmlets()
    {
        // base directory/vendor directory/product directory/firmware version directory/
        // This MUST be kept in sync with how we unpack in UnpackFirmwarePackage().
        var installedFirmletsDirectories = new List<string>();

        var vendors = Directory.GetDirectories(AppLocations.FirmletsPath);
        foreach (var vendor in vendors)
        {
            var products = Directory.GetDirectories(vendor);
            foreach (var product in products)
                installedFirmletsDirectories.AddRange(Directory.GetDirectories(product));
        }

        return installedFirmletsDirectories;
    }

    private Task WriteFirmwareListAsync(CancellationToken cancellationToken)
    {
        // Paths in this file are relative to the cache path (which is binded to the container). We also
        // need to use '/' as path separator because the container is Linux-based (but the system host could run on Windows)
        var list = new List<string>();
        foreach (var firmlet in _firmlets)
            list.Add($"{firmlet.Manifest.FirmwareId}=\"{Path.GetRelativePath(AppLocations.FirmletsPath, firmlet.Path).Replace('\\', '/')}\"");
        return File.WriteAllLinesAsync(Path.Combine(AppLocations.FirmletsPath, "firmlets"), list, cancellationToken);
    }
}