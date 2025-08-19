using Azure.Storage.Blobs;
using System.IO.Compression;
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
        var tempPath = Path.Combine(Path.GetTempPath(), jobId);
        _logger.LogInformation("Starting compilation job {JobId} in {Path}", jobId, tempPath);
        Directory.CreateDirectory(tempPath);

        try
        {
            await DownloadSourceBlob(request, tempPath, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            await _compiler.CompileAsync(
                new Compiler.Request(jobId, tempPath, request.Architecture, [(InputFileName, OutputFileName)]),
                cancellationToken);

            _logger.LogInformation("Creating ZIP archive");
            var zipStream = new MemoryStream();
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                foreach (var filePath in Directory.GetFiles(tempPath))
                {
                    string fileName = Path.GetFileName(filePath);
                    _logger.LogInformation("Adding file {FileName} to archive", fileName);
                    archive.CreateEntryFromFile(filePath, fileName);
                }
            }
            zipStream.Position = 0;
            return zipStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during compilation job {JobId}", jobId);
            throw;
        }
        finally
        {
            _logger.LogInformation("Cleaning up temporary directory {Path}", tempPath);
            Directory.Delete(tempPath, recursive: true);
        }
    }

    private const string InputFileName = "firmware.wasm";
    private const string OutputFileName = "firmware.aot";

    private readonly Compiler _compiler;
    private readonly BlobContainerClient _buildArtifacts;
    private readonly BlobContainerClient _sourceArtifacts;
    private readonly ILogger<CompilationService> _logger;

    private async Task DownloadSourceBlob(CompilationRequest request, string tempPath, CancellationToken cancellationToken)
    {
        var sourceBlobClient = _sourceArtifacts.GetBlobClient(request.BlobName);
        var sourceFilePath = Path.Combine(tempPath, InputFileName);
        _logger.LogInformation("Downloading {BlobName} from {BlobUri} to {Destination}", request.BlobName, sourceBlobClient.Uri, sourceFilePath);
        await sourceBlobClient.DownloadToAsync(sourceFilePath, cancellationToken);
    }
}
