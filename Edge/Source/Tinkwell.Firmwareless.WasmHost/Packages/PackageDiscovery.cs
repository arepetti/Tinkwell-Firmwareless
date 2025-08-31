using Microsoft.Extensions.Logging;

namespace Tinkwell.Firmwareless.WasmHost.Packages;

sealed class PackageDiscovery(ILogger<PackageDiscovery> logger, IPackageValidator validator) : IPackageDiscovery
{
    public async Task<IEnumerable<string>> DiscoverAsync(string baseDirectory, CancellationToken cancellationToken)
    {
        var firmletsDirectory = Path.Combine(baseDirectory, Names.FirmletsDirectory);
        if (!Directory.Exists(firmletsDirectory))
        {
            _logger.LogWarning("Firmlets directory not found: {Path}", firmletsDirectory);
            return [];
        }

        List<string> validFirmlets = [];
        var firmlets = Directory.EnumerateFiles(firmletsDirectory, Names.FirmletsSearchPattern, SearchOption.AllDirectories);
        foreach (var file in firmlets)
        {
            if (await IsValidFirmletAsync(file, cancellationToken))
                validFirmlets.Add(file);
            else
                _logger.LogWarning("Invalid firmlet archive, skipped: {Path}", file);
        }

        return validFirmlets;
    }

    private readonly ILogger<PackageDiscovery> _logger = logger;
    private readonly IPackageValidator _validator = validator;

    private async ValueTask<bool> IsValidFirmletAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await _validator.ValidateAsync(path, cancellationToken);
            return true;
        }
        catch (FirmwareValidationException e)
        {
            _logger.LogWarning(e, "Firmware validation failed: {Message}", e.Message);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unexpected error during firmware validation for {Path}: {Message}", path, e.Message);
        }

        return false;
    }
}