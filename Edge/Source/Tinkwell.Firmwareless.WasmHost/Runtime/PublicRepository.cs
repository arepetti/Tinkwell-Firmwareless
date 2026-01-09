using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using Tinkwell.Firmwareless.WasmHost.Packages;

namespace Tinkwell.Firmwareless.WasmHost.Runtime;

sealed class PublicRepository(ILogger<PublicRepository> logger, IOptions<Settings> settings, HttpClient httpClient) : IPublicRepository
{
    public Task<string> DownloadFirmwareAsync(ProductEntry product, CancellationToken cancellationToken)
    {
        var url = $"{_settings.PublicRepositoryUrl}/api/v1/firmwares/download";

        var request = new
        {
            vendorId = product.VendorId,
            productId = product.ProductId,
            type = product.Type.ToString().ToLowerInvariant(),
            hardwareVersion = FirmwarelessHostInformation.Default.HardwareVersion,
            hardwareArchitecture = FirmwarelessHostInformation.Default.HardwareArchitecture,
        };

        return Try(async () =>
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
            product.FirmwareVersion = PackageManifestReader.Read(outputPath).FirmwareVersion;
            return outputPath;
        }, cancellationToken);
    }

    public Task<string> GetLatestFirmletVersionAsync(ProductEntry product, CancellationToken cancellationToken)
    {
        var url = $"{_settings.PublicRepositoryUrl}/api/v1/firmwares/version/{product.VendorId}/{product.ProductId}?type={product.Type}";

        return Try(async () =>
        {
            var latestVersion = await _httpClient.GetStringAsync(url, cancellationToken);
            _logger.LogDebug("Latest version of {Product} is {Version}", product.ProductId, latestVersion);
            return latestVersion;

        }, cancellationToken);
    }

    public Task<string> GetPublicKeyAsync(CancellationToken cancellationToken)
    {
        var cachedPemPath = Path.Combine(AppLocations.ConfigurationPath, "repository_public_key.pem");
        var url = $"{_settings.PublicRepositoryUrl}/api/v1/repository/identity";

        return Try(async () =>
        {
            if (File.Exists(cachedPemPath))
                return await File.ReadAllTextAsync(cachedPemPath, cancellationToken);

            var pem = await _httpClient.GetStringAsync(url, cancellationToken);
            await File.WriteAllTextAsync(cachedPemPath, pem, cancellationToken);
            return pem;

        }, cancellationToken);
    }

    private const int NumberOfAttempts = 3;
    private const int DelayBetweenAttempts = 1000;

    private readonly ILogger<PublicRepository> _logger = logger;
    private readonly HttpClient _httpClient = httpClient;
    private readonly Settings _settings = settings.Value;

    private async Task<T> Try<T>(Func<Task<T>> action, CancellationToken cancellationToken, [CallerMemberName] string callerName = "")
    {
        for (int i = 1; i <= NumberOfAttempts; ++i)
        {
            try
            {
                return await action();
            }
            catch (HttpRequestException e) when (e.StatusCode is null || e.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
            {
                if (i != NumberOfAttempts)
                {
                    _logger.LogWarning(e, "HTTP error occurred ({Message}), attempt {Attempt}/{NumberOfAttempts}", e.Message, i, NumberOfAttempts);
                    await Task.Delay(DelayBetweenAttempts * i);
                }
                else
                {
                    _logger.LogError(e, "HTTP error occurred when calling {Caller}(): {Message}", callerName, e.Message);
                    throw new WasmHostException($"HTTP error occurred when calling {callerName}(): {e.Message}", e);
                }
            }
            catch (IOException e)
            {
                if (i != NumberOfAttempts)
                {
                    _logger.LogWarning(e, "I/O error occurred ({Message}), attempt {Attempt}/{NumberOfAttempts}", e.Message, i, NumberOfAttempts);
                    await Task.Delay(DelayBetweenAttempts);
                }
                else
                {
                    _logger.LogError(e, "I/O error occurred when calling {Caller}(): {Message}", callerName, e.Message);
                    throw new WasmHostException($"I/O error occurred when calling {callerName}(): {e.Message}", e);
                }
            }
        }

        throw new InvalidOperationException("Unreachable code");
    }
}