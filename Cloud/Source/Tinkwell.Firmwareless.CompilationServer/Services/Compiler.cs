using Docker.DotNet;
using Docker.DotNet.Models;
using System.Text.Json;

namespace Tinkwell.Firmwareless.CompilationServer.Services;

public sealed class Compiler
{
    public sealed record Request(string JobId, string WorkingDirectory, string Target)
    {
        public required CompilationManifest Manifest { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
        public required Func<string, string> GetOutputFileName { get; set; }
        public string? StackUsageFile { get; set; }
        public bool VerboseLog { get; set; }
    };

    public Compiler(IDockerClient dockerClient, ILogger<Compiler> logger, IConfiguration configuration)
    {
        _dockerClient = dockerClient;
        _logger = logger;
        _compilerImageName = configuration["CompilerImageName"] ?? Names.CompilerImageName;

        _containerLimits = new(
            configuration.GetValue("ContainerMemoryLimit", DefaultLimits.Memory),
            configuration.GetValue("ContainerMemorySwapLimit", DefaultLimits.MemorySwap),
            configuration.GetValue("ContainerNanoCpuLimit", DefaultLimits.NanoCpus),
            configuration.GetValue("ContainerPidsLimit", DefaultLimits.Pids),
            configuration.GetValue("ContainerFilesLimit", DefaultLimits.Files)
        );
    }

    public async Task<bool> CompileAsync(Request request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting compilation job {JobId} in {Path} using {ImageName}", request.JobId, request.WorkingDirectory, _compilerImageName);

        await CreateCompilationScript(request, cancellationToken);
        var (success, stdout, stderr) = await RunCompilerContainerAsync(request.WorkingDirectory, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(request.WorkingDirectory, Names.CompilerStdoutFileName), stdout, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(request.WorkingDirectory, Names.CompilerStderrFileName), stderr, cancellationToken);

        request.Metadata.Add("compiler_name", "wamrc");
        request.Metadata.Add("compiler_success", success.ToString().ToLowerInvariant());
        var metadata = JsonSerializer.Serialize(request.Metadata, JsonDefaults.Options);
        await File.WriteAllTextAsync(Path.Combine(request.WorkingDirectory, Names.CompiledFirmwareManifestEntryName), metadata, cancellationToken);

        return success;
    }

    sealed record ContainerLimits(long Memory, long MemorySwap, long NanoCPUs, int Pids, int Files);

    private const string ContainerWamrcPath = "/usr/local/wamr/bin/wamrc";
    private const string ContainerWorkingDirectory = "/app";
    private const string CompilationScriptName = "compile.sh";

    private static System.Text.UTF8Encoding TextEncoding = new System.Text.UTF8Encoding(false);

    private readonly IDockerClient _dockerClient;
    private readonly ILogger<Compiler> _logger;
    private readonly string _compilerImageName;
    private readonly ContainerLimits _containerLimits;

    private static string[] GetCompilerArgs(Request request, string input, string output)
    {
        var builder = new CompilerOptionsBuilder(CompilationTarget.Parse(request.Target));
        builder.WithFiles([($"{ContainerWorkingDirectory}/{input}", $"{ContainerWorkingDirectory}/{output}")]);
        builder.UseMetaArchitectures(Names.CompilerMetaArchitecturesFileName);
        builder.UseValidation(Names.CompilerTargetValidationFileName);
        builder.UseCompilerConfiguration(Names.CompilerTargetOptionsFileName);

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

        CreateContainerResponse container = await CreateSecuredWamrcContainer(hostPath, cancellationToken);

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

    private async Task CreateCompilationScript(Request request, CancellationToken cancellationToken)
    {
        // When should we move to a template file instead of manually building the script here?
        _logger.LogInformation("Preparing the compilation script for {JobId}", request.JobId);
        List<string> compilationScript = ["#!/bin/bash\r\n", "set -e"];
        foreach (var unit in request.Manifest.CompilationUnits)
        {
            compilationScript.Add($"while [ ! -f \"{ContainerWorkingDirectory}/{unit}\" ]; do sleep 1; done");
            compilationScript.Add($"wasm-validate --enable-all \"{ContainerWorkingDirectory}/{unit}\"");
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

    private async Task<CreateContainerResponse> CreateSecuredWamrcContainer(string hostPath, CancellationToken cancellationToken)
    {
        string compilationScriptPathInContainer = $"{ContainerWorkingDirectory}/{CompilationScriptName}";
        var container = await _dockerClient.Containers.CreateContainerAsync(
            new CreateContainerParameters
            {
                Image = _compilerImageName,
                Cmd =
                [
                    "bash", "-c", $"chmod +x {compilationScriptPathInContainer} && {compilationScriptPathInContainer}"
                ],
                WorkingDir = ContainerWorkingDirectory,
                HostConfig = new HostConfig
                {
                    // Bind mount for /app (writable)
                    Binds =
                    [
                        $"{hostPath}:{ContainerWorkingDirectory}"
                    ],

                    // Everything else is read-only
                    ReadonlyRootfs = true,

                    // Writable tmpfs mounts
                    Tmpfs = new Dictionary<string, string>
                    {
                        { "/tmp", "" },
                        { "/var/tmp", "" },
                        { "/var/cache", "" }
                    },

                    Memory = _containerLimits.Memory,
                    MemorySwap = _containerLimits.MemorySwap,
                    NanoCPUs = _containerLimits.NanoCPUs,
                    PidsLimit = _containerLimits.Pids,
                    Ulimits =
                    [
                        new() { Name = "nofile", Soft = _containerLimits.Files, Hard = _containerLimits.Files }
                    ]
                }
            },
            cancellationToken
        );
        return container;
    }
}
