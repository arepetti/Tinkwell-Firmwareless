using Azure.Storage.Blobs;
using System.IO.Compression;
using System.Text.Json;
using Tinkwell.Firmwareless.Controllers;

namespace Tinkwell.Firmwareless.CompilationServer.Services;

public class CompilationService : ICompilationService
{
    public CompilationService(ILogger<CompilationService> logger, IBlobContainerClientFactory blobFactory, CompiledFirmwareArchive archive, Compiler compiler)
    {
        _compiler = compiler;
        _sourceArtifacts = blobFactory.GetBlobContainerClient("tinkwell-firmwarestore-assets");
        //_buildArtifacts = blobFactory.GetBlobContainerClient("tinkwell-firmwarestore-builds");
        _logger = logger;
        _archive = archive;
    }

    public async Task<Stream> CompileAsync(CompilationRequest request, CancellationToken cancellationToken)
    {
        using var job = new CompilationJob();
        _logger.LogInformation("Starting compilation job {JobId} in {Path}", job.Id, job.WorkingDirectoryPath);

        try
        {
            var (manifest, metadata) = await DownloadSourceBlob(request, job.WorkingDirectoryPath, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var parameters = new Compiler.Request(job.Id, job.WorkingDirectoryPath, request.Architecture)
            { 
                GetOutputFileName = GetOutputFileName,
                Manifest = manifest,
                Metadata = metadata,
            };

            await _compiler.CompileAsync(parameters, cancellationToken);
            return await PackageOutputAsZipAsync(job.WorkingDirectoryPath, manifest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during compilation job {JobId}", job.Id);
            throw;
        }
    }

    private const string CompilationOptionsFileName = "firmware.json";
    private const string CompiledFirmwareManifestFileName = "package.json";
    private const string AssetsDirectoryName = "assets";
    private const long MaximumFirmwareSize = 16 * 1024 * 1024; // 16 MB
    private const long MaximumAssetSize = 4 * 1024 * 1024; // 4 MB

    private readonly Compiler _compiler;
    //private readonly BlobContainerClient _buildArtifacts;
    private readonly BlobContainerClient _sourceArtifacts;
    private readonly ILogger<CompilationService> _logger;
    private readonly CompiledFirmwareArchive _archive;

    private async Task<(CompilationManifest Manifest, Dictionary<string, string> Metadata)> DownloadSourceBlob(CompilationRequest request, string workingDirectory, CancellationToken cancellationToken)
    {
        var sourceBlobClient = _sourceArtifacts.GetBlobClient(request.BlobName);
        var downloadedFirmwarePath = Path.Combine(workingDirectory, AssetsDirectoryName, request.BlobName);

        _logger.LogInformation("Downloading {BlobName} from {BlobUri} to {Destination}", request.BlobName, sourceBlobClient.Uri, downloadedFirmwarePath);
        await sourceBlobClient.DownloadToAsync(downloadedFirmwarePath, cancellationToken);
        var tags = await sourceBlobClient.GetTagsAsync(cancellationToken: cancellationToken);

        if (FileTypeDetector.Detect(downloadedFirmwarePath) != FileTypeDetector.FileType.Zip)
            throw new ArgumentException("Unsupported file type for compilation.");

        return HandleZippedFirmware(request, tags.Value.Tags, workingDirectory, downloadedFirmwarePath);
    }

    private static string GetOutputFileName(string inputFileName)
        => Path.ChangeExtension(Path.GetFileName(inputFileName), ".aot");

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

        foreach (var compilationUnit in manifest.CompilationUnits)
        {
            if (IsSafeRelativePath(compilationUnit) == false)
                throw new ArgumentException($"Invalid compilation unit path: {compilationUnit}. It must be a relative path.");

            string outputPath = Path.Combine(workingDirectory, compilationUnit);
            _logger.LogDebug("Extracting {OutputPath}...", outputPath);

            var entry = archive.GetEntry(compilationUnit);
            if (entry is null)
                throw new FileNotFoundException($"Required file {compilationUnit} not found in the archive.");

            if (entry.Length > MaximumFirmwareSize)
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

            if (entry.Length > MaximumAssetSize)
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
            var manifestEntry = archive.GetEntry(CompilationOptionsFileName);
            if (manifestEntry is not null)
            {
                using var stream = manifestEntry.Open();
                return JsonSerializer.Deserialize<CompilationManifest>(stream, JsonDefaults.Options)!;
            }

            return new CompilationManifest();
        }
    }

    private async Task<Stream> PackageOutputAsZipAsync(string workingDirectory, CompilationManifest manifest, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Packaging compilation output as zip archive");

        foreach (var unit in manifest.CompilationUnits)
        {
            _logger.LogDebug("Adding file {FileName} to archive", unit);
            await _archive.AddFileAsync(Path.Combine(workingDirectory, unit), $"src/{unit}", cancellationToken);

            string compiledUnitPath = Path.Combine(workingDirectory, GetOutputFileName(unit));
            string compiledUnitFileName = Path.GetFileName(compiledUnitPath);

            _logger.LogDebug("Adding file {FileName} to archive", compiledUnitFileName);
            await _archive.AddFileAsync(compiledUnitPath, compiledUnitFileName, cancellationToken);
        }

        foreach (var sourceRelativePath in manifest.Assets)
        {
            _logger.LogDebug("Adding file {FileName} to archive", sourceRelativePath);
            var assetFilePath = Path.Combine(workingDirectory, sourceRelativePath);
            var assetFileName = $"{AssetsDirectoryName}/{sourceRelativePath}";
            await _archive.AddFileAsync(assetFilePath, assetFileName, cancellationToken);
        }

        var firmwareJsonPath = Path.Combine(workingDirectory, CompiledFirmwareManifestFileName);
        if (File.Exists(firmwareJsonPath))
            await _archive.AddFileAsync(firmwareJsonPath, CompiledFirmwareManifestFileName, cancellationToken);

        var stdoutPath = Path.Combine(workingDirectory, "stdout.txt");
        if (File.Exists(stdoutPath))
            await _archive.AddFileAsync(stdoutPath, "log/stdout.txt", cancellationToken);

        var stderrPath = Path.Combine(workingDirectory, "stderr.txt");
        if (File.Exists(stderrPath))
            await _archive.AddFileAsync(stderrPath, "log/stderr.txt", cancellationToken);

        await _archive.FreezeAsync(cancellationToken);

        return _archive.Stream;
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
