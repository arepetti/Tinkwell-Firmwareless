using Azure.Identity;
using Azure.Storage.Blobs;
using Docker.DotNet;
using Docker.DotNet.Models;
using System.IO.Compression;

namespace Tinkwell.Firmwareless.CompilationServer.Services;

public class CompilationService : ICompilationService
{
    public CompilationService(IDockerClient dockerClient, BlobContainerClient blob, ILogger<CompilationService> logger, IConfiguration configuration)
    {
        _dockerClient = dockerClient;
        _blob = blob;
        _logger = logger;
        _compilerImageName = configuration["CompilerImageName"] ?? "wamrc-compiler:latest";
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

            (string outputFilePath, string stdoutFilePath, string stderrFilePath) =
                await RunCompilerAndCollectOutput(request, tempPath, cancellationToken);

            // Upload result to cache blob
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(outputFilePath))
            {
                _logger.LogInformation("Uploading compiled artifact to cache");
                var cacheBlobClient = _blob.GetBlobClient($"{jobId}-{request.Architecture}.aot");
                await cacheBlobClient.UploadAsync(outputFilePath, true, cancellationToken);
            }
            else
            {
                _logger.LogWarning("Output file 'output.aot' not found after compilation.");
            }

            // Create ZIP archive and return as a stream
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("Creating ZIP archive");
            var zipStream = new MemoryStream();
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                if (File.Exists(outputFilePath))
                    archive.CreateEntryFromFile(outputFilePath, "output.aot");

                archive.CreateEntryFromFile(stdoutFilePath, "stdout.txt");
                archive.CreateEntryFromFile(stderrFilePath, "stderr.txt");
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
            // Clean up the temporary directory
            _logger.LogInformation("Cleaning up temporary directory {Path}", tempPath);
            Directory.Delete(tempPath, recursive: true);
        }
    }

    private readonly IDockerClient _dockerClient;
    private readonly BlobContainerClient _blob;
    private readonly ILogger<CompilationService> _logger;
    private readonly string _compilerImageName;

    private async Task DownloadSourceBlob(CompilationRequest request, string tempPath, CancellationToken cancellationToken)
    {
        var sourceBlobClient = new BlobClient(new Uri(request.BlobUrl), new DefaultAzureCredential());
        var sourceFilePath = Path.Combine(tempPath, "source.wasm");
        _logger.LogInformation("Downloading source from {BlobUrl}", request.BlobUrl);
        await sourceBlobClient.DownloadToAsync(sourceFilePath, cancellationToken);
    }

    private async Task<(string outputFilePath, string stdoutFilePath, string stderrFilePath)> RunCompilerAndCollectOutput(CompilationRequest request, string tempPath, CancellationToken cancellationToken)
    {
        var compilerArgs = GetCompilerArgs(request.Architecture);

        // Run the compilation in a container
        _logger.LogInformation("Running compiler container using image {ImageName}", _compilerImageName);
        var (stdout, stderr) = await RunCompilerContainerAsync(tempPath, compilerArgs, cancellationToken);

        var outputFilePath = Path.Combine(tempPath, "output.aot");
        var stdoutFilePath = Path.Combine(tempPath, "stdout.txt");
        var stderrFilePath = Path.Combine(tempPath, "stderr.txt");

        await File.WriteAllTextAsync(stdoutFilePath, stdout, cancellationToken);
        await File.WriteAllTextAsync(stderrFilePath, stderr, cancellationToken);
        return (outputFilePath, stdoutFilePath, stderrFilePath);
    }

    private async Task<(string stdout, string stderr)> RunCompilerContainerAsync(string hostPath, string[] args, CancellationToken cancellationToken)
    {
        // The image name now contains the tag, so we pass it in FromImage
        await _dockerClient.Images.CreateImageAsync(
            new ImagesCreateParameters { FromImage = _compilerImageName }, 
            null, 
            new Progress<JSONMessage>(),
            cancellationToken);

        var container = await _dockerClient.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Image = _compilerImageName,
            Cmd = args,
            HostConfig = new HostConfig
            {
                Binds = new List<string> { $"{hostPath}:/data" },
                AutoRemove = true
            }
        }, cancellationToken);

        await _dockerClient.Containers.StartContainerAsync(container.ID, null, cancellationToken);
        var waitResponse = await _dockerClient.Containers.WaitContainerAsync(container.ID, cancellationToken);

        if (waitResponse.StatusCode != 0)
            _logger.LogError("Compiler container exited with code {StatusCode}", waitResponse.StatusCode);

        using var stream = await _dockerClient.Containers.GetContainerLogsAsync(
            container.ID,
            tty: false,
            new ContainerLogsParameters { ShowStdout = true, ShowStderr = true },
            cancellationToken);

        using var stdin = new StringStream();
        using var stdout = new StringStream();
        using var stderr = new StringStream();
        await stream.CopyOutputToAsync(stdin, stdout, stderr, cancellationToken);

        return (stdout.ConvertToString(), stderr.ConvertToString());
    }

    private string[] GetCompilerArgs(string architecture)
    {
        return architecture.ToLowerInvariant() switch
        {
            "x86_64" => ["--target=x86_64", "-o", "output.aot", "source.wasm"],
            "aarch64" => ["--target=aarch64", "-o", "output.aot", "source.wasm"],
            _ => throw new NotSupportedException($"Architecture '{architecture}' is not supported.")
        };
    }
}
