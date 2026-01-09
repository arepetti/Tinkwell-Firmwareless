using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tinkwell.Firmwareless.WasmHost.Runtime;

namespace Tinkwell.Firmwareless.WasmHost.Packages;

sealed class PackageDiscovery(ILogger<PackageDiscovery> logger, IPackageValidator validator, IPublicRepository repository) : IPackageDiscovery
{
    public async Task<IEnumerable<ProductEntry>> DiscoverAsync(CancellationToken cancellationToken)
    {
        var config = LoadConfiguration();
        await DownloadMissingFirmletsAsync(config, cancellationToken);
        DeleteOrphans(config);
        await ValidateFirmletsAsync(config, cancellationToken);

        return config.Products;
    }

    private readonly ILogger<PackageDiscovery> _logger = logger;
    private readonly IPackageValidator _validator = validator;
    private readonly IPublicRepository _repository = repository;

    private SystemConfiguration LoadConfiguration()
    {
        var path = Path.Combine(AppLocations.ConfigurationPath, Names.SystemConfigurationFileName);
        _logger.LogTrace("Loading system configuration from {Path}", path);

        var content = File.ReadAllText(path);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.Converters.Add(new JsonStringEnumConverter());
        return JsonSerializer.Deserialize<SystemConfiguration>(content, options)!;
    }

    private async Task DownloadMissingFirmletsAsync(SystemConfiguration config, CancellationToken cancellationToken)
    {
        foreach (var product in config.Products)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            if (string.IsNullOrWhiteSpace(product.Package))
                product.Package = await _repository.DownloadFirmwareAsync(product, cancellationToken);
            else
            {
                var latestVersion = await _repository.GetLatestFirmletVersionAsync(product, cancellationToken);
                if (latestVersion is not null && product.FirmwareVersion != latestVersion)
                {
                    _logger.LogInformation("A new version of {Product} is available: {Current} -> {Latest}", product.ProductId, product.FirmwareVersion, latestVersion);
                    product.Package = await _repository.DownloadFirmwareAsync(product, cancellationToken);
                    product.FirmwareVersion = latestVersion;
                }
            }
        }
    }

    private void DeleteOrphans(SystemConfiguration config)
    {
        // Note that this orphans are not the same deleted in FirmletsManager! Here we delete orphan packages (.zip) while in FirmletsManager
        // we delete the unpacked firmlets (directories).
        var existingFirmlets = Directory.EnumerateFiles(AppLocations.FirmletsPackagesPath, Names.FirmletsSearchPattern, SearchOption.AllDirectories);
        var requiredFirmlets = config.Products.Select(x => x.Package!);
        var orphans = existingFirmlets.Except(requiredFirmlets);
        foreach (var orphan in orphans)
        {
            try
            {
                File.Delete(orphan);
                _logger.LogInformation("Deleted orphaned firmlet package: {Path}", orphan);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Cannot delete orphaned firmlet package: {Path}", orphan);
            }
        }
    }

    private async Task ValidateFirmletsAsync(SystemConfiguration config, CancellationToken cancellationToken)
    {
        foreach (var product in config.Products)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            if (!await IsValidFirmletAsync(product.Package!, cancellationToken))
            {
                _logger.LogWarning("Invalid firmlet archive, skipped: {Path}", product.Package);
                product.Disabled = true;
            }
        }
    }

    private async ValueTask<bool> IsValidFirmletAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await _validator.ValidateAsync(path, cancellationToken);
            return true;
        }
        catch (FirmwareValidationException e)
        {
            _logger.LogWarning(e, "Firmware validation of {Path} failed: {Message}", path, e.Message);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unexpected error during firmware validation for {Path}: {Message}", path, e.Message);
        }

        return false;
    }
}

