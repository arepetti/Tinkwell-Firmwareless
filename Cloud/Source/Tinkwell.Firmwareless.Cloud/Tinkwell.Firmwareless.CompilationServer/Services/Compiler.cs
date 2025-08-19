using Docker.DotNet;
using Docker.DotNet.Models;
using Tinkkwell.Firmwareless;

namespace Tinkwell.Firmwareless.CompilationServer.Services;

public sealed class Compiler
{
    public sealed record Request(string JobId, string WorkingDirectory, string Target)
    {
        public required CompilationManifest Manifest {  get; set; }
        public required Func<string, string> GetOutputFileName { get; set; }
        public string? StackUsageFile { get; set; }
        public bool VerboseLog { get; set; }
    };

    public Compiler(IDockerClient dockerClient, ILogger<Compiler> logger, IConfiguration configuration)
    {
        _dockerClient = dockerClient;
        _logger = logger;
        _compilerImageName = configuration["CompilerImageName"] ?? "wamrc-compiler:latest";
    }

    public async Task<bool> CompileAsync(Request request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting compilation job {JobId} in {Path} using {ImageName}", request.JobId, request.WorkingDirectory, _compilerImageName);

        await CreateCompilationScript(request, cancellationToken);
        var (success, stdout, stderr) = await RunCompilerContainerAsync(request.WorkingDirectory, cancellationToken);
        await File.AppendAllTextAsync(Path.Combine(request.WorkingDirectory, "stdout.txt"), stdout, cancellationToken);
        await File.AppendAllTextAsync(Path.Combine(request.WorkingDirectory, "stderr.txt"), stderr, cancellationToken);

        return success;
    }

    private const string ContainerWamrcPath = "/usr/local/wamr/bin/wamrc";
    private const string ContainerWorkingDirectory = "/app";
    private const string CompilationScriptName = "compile.sh";

    private static System.Text.UTF8Encoding TextEncoding = new System.Text.UTF8Encoding(false);
    
    private readonly IDockerClient _dockerClient;
    private readonly ILogger<Compiler> _logger;
    private readonly string _compilerImageName;

    private async Task CreateCompilationScript(Request request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Preparing the compilation script for {JobId}", request.JobId);
        List<string> compilationScript = new();
        foreach (var unit in request.Manifest.CompilationUnits)
        {
            compilationScript.Add($"while [ ! -f \"{ContainerWorkingDirectory}/{unit}\" ]; do sleep 1; done");
            var args = GetCompilerArgs(request, unit, request.GetOutputFileName(unit));
            string[] command = [ContainerWamrcPath, .. args];
            compilationScript.Add(string.Join(' ', command));
            _logger.LogInformation("Command {Command}", string.Join(' ', command));
        }

        await File.WriteAllTextAsync(
            Path.Combine(request.WorkingDirectory, CompilationScriptName),
            string.Join('\n', compilationScript),
            TextEncoding,
            cancellationToken);
    }

    private static string[] GetCompilerArgs(Request request, string input, string output)
    {
        var builder = new CompilerOptionsBuilder(CompilationTarget.Parse(request.Target));
        builder.WithFiles([($"{ContainerWorkingDirectory}/{input}", $"{ContainerWorkingDirectory}/{output}")]);
        builder.UseMetaArchitectures("meta-architectures.yml");
        builder.UseValidation("target-validation.yml");
        builder.UseCompilerConfiguration("target-options.yml");

        var options = new CompilerOptionsBuilderOptions
        {
            StackUsageFile = request.StackUsageFile,
            EnableMultiThread = request.Manifest.EnableMultiThread,
            EnableTailCall = request.Manifest.EnableTailCall,
            EnableGarbageCollection = request.Manifest.EnableGarbageCollection,
            VerboseLog = request.VerboseLog
        };

        return builder.Build(options);
    }

    private async Task<(bool success, string stdout, string stderr)> RunCompilerContainerAsync(string hostPath, CancellationToken cancellationToken)
    {
        // Check that the compiler image exists locally
        _logger.LogDebug("Searching for {ImageName}", _compilerImageName);
        var images = await _dockerClient.Images.ListImagesAsync(new ImagesListParameters { All = true });
        if (!images.Any(x => x.RepoTags.Contains(_compilerImageName)))
            throw new InvalidOperationException($"Image {_compilerImageName} not found locally");

        // Run the compiler. We're using a compilation script not only to support multiple compilation units
        // but also because, when running on Windows in local development, the host directory won't be visible.
        // It's not even about a short delay (it doesn't change anything), probably a race condition in the docker
        // client library or the Docker service itself. Executing a script (or even the compiler itself) through bash
        // seems to work around this issue.
        string compilationScriptPathInContainer = $"{ContainerWorkingDirectory}/{CompilationScriptName}";
        var container = await _dockerClient.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Image = _compilerImageName,
            Cmd = ["bash", "-c", $"chmod +x {compilationScriptPathInContainer} && {compilationScriptPathInContainer}"],
            HostConfig = new HostConfig { Binds = [$"{hostPath}:{ContainerWorkingDirectory}"] },
            WorkingDir = ContainerWorkingDirectory,
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

        // Collect output
        using var stdin = new StringStream();
        using var stdout = new StringStream();
        using var stderr = new StringStream();
        await stream.CopyOutputToAsync(stdin, stdout, stderr, cancellationToken);

        if (waitResponse.StatusCode != 0)
        {
            _logger.LogWarning(stdout.ConvertToString());
            _logger.LogWarning(stderr.ConvertToString());
        }

        _logger.LogDebug("Cleaning up container {ImageName}", container.ID);
        await _dockerClient.Containers.RemoveContainerAsync(
            container.ID,
            new ContainerRemoveParameters { Force = true },
            cancellationToken);

        return (waitResponse.StatusCode == 0, stdout.ConvertToString(), stderr.ConvertToString());
    }
}
