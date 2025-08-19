using Azure.Storage.Blobs;
using System.IO.Compression;
using System.Text.Json;
using Tinkkwell.Firmwareless;
using Tinkwell.Firmwareless.Controllers;

namespace Tinkwell.Firmwareless.CompilationServer.Services;

public class CompilationService : ICompilationService
{
    public CompilationService(ILogger<CompilationService> logger, IBlobContainerClientFactory blobFactory, Compiler compiler)
    {
        _compiler = compiler;
        _sourceArtifacts = blobFactory.GetBlobContainerClient("tinkwell-firmwarestore-assets");
        _buildArtifacts = blobFactory.GetBlobContainerClient("tinkwell-firmwarestore-builds");
        _logger = logger;
    }

    public async Task<Stream> CompileAsync(CompilationRequest request, CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid().ToString("N");
        var workingDirectory = Path.Combine(Path.GetTempPath(), jobId);
        _logger.LogInformation("Starting compilation job {JobId} in {Path}", jobId, workingDirectory);

        Directory.CreateDirectory(workingDirectory);
        Directory.CreateDirectory(Path.Combine(workingDirectory, AssetsDirectoryName));

        try
        {
            var manifest = await DownloadSourceBlob(request, workingDirectory, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var parameters = new Compiler.Request(jobId, workingDirectory, request.Architecture)
            { 
                GetOutputFileName = GetOutputFileName,
                Manifest = manifest
            };

            await _compiler.CompileAsync(parameters, cancellationToken);
            return PackageOutputAsZip(workingDirectory, manifest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during compilation job {JobId}", jobId);
            throw;
        }
        finally
        {
            _logger.LogInformation("Cleaning up temporary directory {Path}", workingDirectory);
            try
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Error deleting directory content: {Reason}", e.Message);
            }
        }
    }

    private const string DefaultInputFileName = "firmware.wasm";
    private const string AssetsDirectoryName = "assets";

    private readonly Compiler _compiler;
    private readonly BlobContainerClient _buildArtifacts;
    private readonly BlobContainerClient _sourceArtifacts;
    private readonly ILogger<CompilationService> _logger;

    private async Task<CompilationManifest> DownloadSourceBlob(CompilationRequest request, string workingDirectory, CancellationToken cancellationToken)
    {
        var sourceBlobClient = _sourceArtifacts.GetBlobClient(request.BlobName);
        var downloadedFirmwarePath = Path.Combine(workingDirectory, AssetsDirectoryName, request.BlobName);

        _logger.LogInformation("Downloading {BlobName} from {BlobUri} to {Destination}", request.BlobName, sourceBlobClient.Uri, downloadedFirmwarePath);
        await sourceBlobClient.DownloadToAsync(downloadedFirmwarePath, cancellationToken);

        return FileTypeDetector.Detect(downloadedFirmwarePath) switch
        {
            FileTypeDetector.FileType.Wasm => HandleSingleFileFirmware(request, workingDirectory, downloadedFirmwarePath),
            FileTypeDetector.FileType.Zip => HandleZippedFirmware(request, workingDirectory, downloadedFirmwarePath),
            _ => throw new ArgumentException("Unsupported file type for compilation."),
        };
    }

    private static string GetOutputFileName(string inputFileName)
        => Path.ChangeExtension(Path.GetFileName(inputFileName), ".aot");

    private static CompilationManifest HandleSingleFileFirmware(CompilationRequest request, string workingDirectory, string firmwareFilePath)
    {
        string defaultPath = Path.Combine(workingDirectory, DefaultInputFileName);
        File.Move(firmwareFilePath, defaultPath, overwrite: true);
        var manifest = new CompilationManifest();
        manifest.CompilationUnits.Add(DefaultInputFileName);
        return manifest;
    }

    private CompilationManifest HandleZippedFirmware(CompilationRequest request, string workingDirectory, string zipFilePath)
    {
        using var archive = ZipFile.OpenRead(zipFilePath);
        var manifest = ExtractOrCreateManifest();

        foreach (var compilationUnit in manifest.CompilationUnits)
        {
            if (IsSafeRelativePath(compilationUnit) == false)
                throw new ArgumentException($"Invalid compilation unit path: {compilationUnit}. It must be a relative path.");

            string outputPath = Path.Combine(workingDirectory, compilationUnit);
            _logger.LogDebug("Extracting {OutputPath}...", workingDirectory);

            var entry = archive.GetEntry(compilationUnit);
            if (entry is null)
                throw new FileNotFoundException($"Required file {compilationUnit} not found in the archive.");

            entry.ExtractToFile(outputPath, overwrite: true);
        }

        return manifest;

        CompilationManifest ExtractOrCreateManifest()
        {
            var manifestEntry = archive.GetEntry("firmware.json");
            if (manifestEntry is not null)
            {
                using var stream = manifestEntry.Open();
                return JsonSerializer.Deserialize<CompilationManifest>(stream, JsonDefaults.Options)!;
            }

            return new CompilationManifest();
        }
    }

    private Stream PackageOutputAsZip(string workingDirectory, CompilationManifest manifest)
    {
        _logger.LogInformation("Packaging compilation output as zip archive");
        var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
        {
            foreach (var unit in manifest.CompilationUnits)
            {
                _logger.LogDebug("Adding file {FileName} to archive", unit);
                archive.CreateEntryFromFile(Path.Combine(workingDirectory, unit), $"source/{unit}");

                string compiledUnitPath = Path.Combine(workingDirectory, GetOutputFileName(unit));
                string compiledUnitFileName = Path.GetFileName(compiledUnitPath);
                _logger.LogDebug("Adding file {FileName} to archive", compiledUnitFileName);
                archive.CreateEntryFromFile(compiledUnitPath, compiledUnitFileName);
            }

            var stdoutPath = Path.Combine(workingDirectory, "stdout.txt");
            if (File.Exists(stdoutPath))
                archive.CreateEntryFromFile(stdoutPath, "log/stdout.txt");

            var stderrPath = Path.Combine(workingDirectory, "stderr.txt");
            if (File.Exists(stderrPath))
                archive.CreateEntryFromFile(stderrPath, "log/stderr.txt");
        }
        zipStream.Position = 0;
        return zipStream;
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
}
