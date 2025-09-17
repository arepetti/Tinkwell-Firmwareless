using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Runtime.InteropServices;

namespace Tinkwell.Firmwareless.WasmHost.Runtime;

sealed class PublicRepository(ILogger<PublicRepository> logger, IOptions<Settings> settings, HttpClient httpClient) : IPublicRepository
{
    public async Task<string> DownloadFirmwareAsync(ProductEntry product, CancellationToken cancellationToken)
    {
        var url = $"{_settings.PublicRepositoryUrl}/api/v1/firmwares/download";

        var request = new
        {
            vendorId = product.VendorId,
            productId = product.ProductId,
            type = product.Type.ToString().ToLowerInvariant(),
            hardwareVersion = "1.0",
            hardwareArchitecture = GetHubAotTargetArchitecture(),
        };

        // TODO: retry logic
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var outputFileName = $"firmlet-{product.ProductId}.zip";
            if (response.Content.Headers.ContentDisposition is not null)
            {
                var suggestedFileName = response.Content.Headers.ContentDisposition.FileName?.Trim('"');
                if (!string.IsNullOrWhiteSpace(suggestedFileName))
                    outputFileName = suggestedFileName;
            }

            var outputPath = Path.Combine(AppLocations.FirmletsPackagesPath, outputFileName);
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await stream.CopyToAsync(fileStream, cancellationToken);

            _logger.LogInformation("Download complete: {Path}", outputPath);
            return outputPath;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to download firmware from {Url}, reason: {Reason}", url, e.Message);
            throw new WasmHostException(e.Message, e);
        }
    }

    public async Task<string> GetPublicKeyAsync(CancellationToken cancellationToken)
    {
        var cachedPemPath = Path.Combine(AppLocations.ConfigurationPath, "repository_public_key.pem");
        var url = $"{_settings.PublicRepositoryUrl}/api/v1/repository/identity";

        // TODO: retry logic
        try
        {
            if (File.Exists(cachedPemPath))
                return await File.ReadAllTextAsync(cachedPemPath, cancellationToken);

            var pem = await _httpClient.GetStringAsync(url, cancellationToken);
            await File.WriteAllTextAsync(cachedPemPath, pem, cancellationToken);
            return pem;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to retrieve public key from {Url}, reason: {Reason}", url, e.Message);
            throw new WasmHostException(e.Message, e);
        }
    }

    private readonly ILogger<PublicRepository> _logger = logger;
    private readonly HttpClient _httpClient = httpClient;
    private readonly Settings _settings = settings.Value;

    private static string GetHubAotTargetArchitecture()
    {
        if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "windows";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return "linux";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "macos";
        }

        if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "aarch64-pc-windows-msvc";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return "aarch64-pc-linux-gnu";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "aarch64-apple-darwing-gnu";
        }

        // There are MANY more possible combinations, this is just for a reference.
        // If we do not want to customize the build then we could add the target architecture as setting.
        throw new PlatformNotSupportedException("Unsupported OS platform");
    }
}