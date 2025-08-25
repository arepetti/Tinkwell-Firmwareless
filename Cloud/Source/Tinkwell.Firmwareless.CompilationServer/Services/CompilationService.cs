using Tinkwell.Firmwareless.Controllers;

namespace Tinkwell.Firmwareless.CompilationServer.Services;

public sealed class CompilationService : ICompilationService
{
    public CompilationService(ILogger<CompilationService> logger, FirmwareSourcePackage sourceArchive, CompiledFirmwarePackage targetArchive, Compiler compiler)
    {
        _compiler = compiler;
        _logger = logger;
        _sourceArchive = sourceArchive;
        _targetArchive = targetArchive;
    }

    public async Task<Stream> CompileAsync(CompilationRequest request, CancellationToken cancellationToken)
    {
        using var job = new CompilationJob(request);
        _logger.LogInformation("Starting compilation job {JobId} in {Path}", job.Id, job.WorkingDirectoryPath);

        try
        {
            var (manifest, metadata) = await _sourceArchive.DownloadAsync(job, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var parameters = new Compiler.Request(job.Id, job.WorkingDirectoryPath, request.Architecture)
            { 
                GetOutputFileName = GetOutputFileName,
                Manifest = manifest,
                Metadata = metadata,
            };

            await _compiler.CompileAsync(parameters, cancellationToken);
            return await _targetArchive.PackageOutputAsync(job, GetOutputFileName, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during compilation job {JobId}", job.Id);
            throw;
        }
    }

    private readonly Compiler _compiler;
    private readonly ILogger<CompilationService> _logger;
    private readonly FirmwareSourcePackage _sourceArchive;
    private readonly CompiledFirmwarePackage _targetArchive;

    private static string GetOutputFileName(string inputFileName)
        => Path.ChangeExtension(Path.GetFileName(inputFileName), ".aot");
}
