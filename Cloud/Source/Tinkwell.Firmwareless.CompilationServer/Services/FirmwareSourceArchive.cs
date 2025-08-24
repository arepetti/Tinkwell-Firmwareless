using Azure.Storage.Blobs;
using System.IO.Compression;
using System.Text.Json;
using Tinkwell.Firmwareless.Controllers;

namespace Tinkwell.Firmwareless.CompilationServer.Services;

public sealed class FirmwareSourceArchive
{
    public FirmwareSourceArchive(ILogger<FirmwareSourceArchive> logger, IBlobContainerClientFactory blobFactory)
    {
        _logger = logger;
        _sourceArtifacts = blobFactory.GetBlobContainerClient("tinkwell-firmwarestore-assets");
    }

    public async Task<(CompilationManifest Manifest, Dictionary<string, string> Metadata)> DownloadAsync(CompilationJob job, CancellationToken cancellationToken)
    {
        var sourceBlobClient = _sourceArtifacts.GetBlobClient(job.Request.BlobName);
        var downloadedFirmwarePath = Path.Combine(job.WorkingDirectoryPath, Names.AssetsDirectoryName, job.Request.BlobName);

        _logger.LogInformation("Downloading {BlobName} from {BlobUri} to {Destination}", job.Request.BlobName, sourceBlobClient.Uri, downloadedFirmwarePath);
        await sourceBlobClient.DownloadToAsync(downloadedFirmwarePath, cancellationToken);
        var tags = await sourceBlobClient.GetTagsAsync(cancellationToken: cancellationToken);

        if (FileTypeDetector.Detect(downloadedFirmwarePath) != FileTypeDetector.FileType.Zip)
            throw new ArgumentException("Unsupported file type for compilation.");

        return HandleZippedFirmware(job.Request, tags.Value.Tags, job.WorkingDirectoryPath, downloadedFirmwarePath);
    }

    private readonly ILogger<FirmwareSourceArchive> _logger;
    private readonly BlobContainerClient _sourceArtifacts;

    private (CompilationManifest Manifest, Dictionary<string, string> Metadata) HandleZippedFirmware(CompilationRequest request, IDictionary<string, string> tags, string workingDirectory, string zipFilePath)
    {
        // First of all validate the package integrity and authenticity
        if (tags.TryGetValue("certificate", out var publicKeyPem))
            FirmwarePackageValidator.Validate(zipFilePath, publicKeyPem);

        // Then we can validate and extract the content
        using var archive = ZipFile.OpenRead(zipFilePath);
        var manifest = ExtractOrCreateManifest();

        if (manifest.CompilationUnits.Count == 0)
            throw new ArgumentException("The archive must contain at least one compilation unit.");

        if (new HashSet<string>(manifest.CompilationUnits).Count != manifest.CompilationUnits.Count)
            throw new ArgumentException("Each compilation unit must be unique.");

        if (new HashSet<string>(manifest.Assets).Count != manifest.Assets.Count)
            throw new ArgumentException("Each asset must be unique.");

        if (manifest.CompilationUnits.Count > DefaultLimits.MaxiumCompilationUnits)
            throw new ArgumentException($"Too many compilation units, maximum allowed is {DefaultLimits.MaxiumCompilationUnits}.");

        if (manifest.Assets.Count > DefaultLimits.MaximumAssets)
            throw new ArgumentException($"Too many assets, maximum allowed is {DefaultLimits.MaximumAssets}.");

        foreach (var compilationUnit in manifest.CompilationUnits)
        {
            if (IsSafeRelativePath(compilationUnit) == false)
                throw new ArgumentException($"Invalid compilation unit path: {compilationUnit}. It must be a relative path.");

            string outputPath = Path.Combine(workingDirectory, compilationUnit);
            _logger.LogDebug("Extracting {OutputPath}...", outputPath);

            var entry = archive.GetEntry(compilationUnit);
            if (entry is null)
                throw new FileNotFoundException($"Required file {compilationUnit} not found in the archive.");

            if (entry.Length > DefaultLimits.MaximumFirmwareSize)
                throw new ArgumentException($"Compilation unit {compilationUnit} is too big: {entry.Length} bytes.");

            entry.ExtractToFile(outputPath, overwrite: true);
        }

        foreach (var asset in manifest.Assets)
        {
            if (IsSafeRelativePath(asset) == false)
                throw new ArgumentException($"Invalid asset path: {asset}. It must be a relative path.");

            string outputPath = Path.Combine(workingDirectory, asset);
            _logger.LogDebug("Extracting {OutputPath}...", outputPath);

            var entry = archive.GetEntry(asset);
            if (entry is null)
                throw new FileNotFoundException($"Required file {asset} not found in the archive.");

            if (entry.Length > DefaultLimits.MaximumAssetSize)
                throw new ArgumentException($"Asset {asset} is too big: {entry.Length} bytes.");

            entry.ExtractToFile(outputPath, overwrite: true);
        }

        // Now we calculate the permissions needed by this firmware according to the manifest
        var permissions = new List<string>();
        if (manifest.EnableGarbageCollection)
            permissions.Add("runtime.gc");
        if (manifest.EnableMultiThread)
            permissions.Add("runtime.mt");
        if (manifest.CompilationUnits.Count > 1)
            permissions.Add("runtime.modules");
        if (manifest.Assets.Count > 0)
            permissions.Add("io.read_assets");

        return (manifest, CreateMetadata(request, tags, permissions));

        CompilationManifest ExtractOrCreateManifest()
        {
            var manifestEntry = archive.GetEntry(Names.InputOptionsFileName);
            if (manifestEntry is not null)
            {
                using var stream = manifestEntry.Open();
                return JsonSerializer.Deserialize<CompilationManifest>(stream, JsonDefaults.Options)!;
            }

            return new CompilationManifest();
        }
    }

    private static bool IsSafeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        // Normalize separators to Linux-style
        var normalized = path.Replace('\\', '/');

        // Reject absolute paths and paths which MIGHT go above the base directory
        if (normalized.StartsWith("/") || normalized.Contains(".."))
            return false;

        // "." only at the beginning (it should be safe anywhere but there is no
        // reason to have it anyway)
        if (normalized.Contains("/./") || normalized.EndsWith("/."))
            return false;

        return true;
    }

    private static Dictionary<string, string> CreateMetadata(CompilationRequest request, IDictionary<string, string> tags, IEnumerable<string> permissions)
    {
        var metadata = tags.ToDictionary(x => x.Key, x => x.Value);

        metadata.Add("schema_version", "1");
        metadata.Add("host_firmware_id", request.BlobName);
        metadata.Add("host_architecture", request.Architecture);
        metadata.Add("permissions", string.Join(',', permissions));

        return metadata;
    }
}
