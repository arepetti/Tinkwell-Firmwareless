using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StreamJsonRpc;
using System.Diagnostics.CodeAnalysis;
using Tinkwell.Firmwareless.WamrAotHost.Hosting;
using Tinkwell.Firmwareless.WamrAotHost.Ipc;
using Tinkwell.Firmwareless.WamrAotHost.Ipc.Requests;

namespace Tinkwell.Firmwareless.WamrAotHost;

sealed record HostServiceOptions(string Path, string Id, string PipeName, bool Transient);

sealed class HostService(IHost host, ILogger<HostService> logger, HostServiceOptions options, IpcClient ipcClient, IWamrHost wamrHost) : BackgroundService
{
    [JsonRpcMethod(HostMethods.Shutdown)]
    public void Shutdown()
    {
        _logger.LogInformation("Shutting down {HostId} (coordinator requested)...", _options.Id);
        _ = _host.StopAsync();
    }

    [DynamicDependency(nameof(Shutdown), typeof(HostService))]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Wamr host {Id}, channel {PipeName}, modules in {Path}.", _options.Id, _options.PipeName, _options.Path);
        _wamrHost.Load(FindAllSourceFiles(_options.Path));

        _logger.LogDebug("Initializing host {HostId}...", _options.Id);
        _wamrHost.InitializeModules();
        _wamrHost.Start();

        _logger.LogDebug("Notifying coordinator through {PipeName}...", _options.PipeName);
        await _ipcClient.StartClientAsync(_options.PipeName, _options.Id, this, stoppingToken);

        _logger.LogInformation("Host {HostId} started", _options.Id);

        if (!_options.Transient)
            stoppingToken.WaitHandle.WaitOne();

        await _ipcClient.DisconnectAsync();
        _wamrHost.Stop();
    }

    private readonly IHost _host = host;
    private readonly ILogger<HostService> _logger = logger;
    private readonly HostServiceOptions _options = options;
    private readonly IpcClient _ipcClient = ipcClient;
    private readonly IWamrHost _wamrHost = wamrHost;

    private string[] FindAllSourceFiles(string path)
    {
        var wasmFiles = FindSourceFiles(path, "*.wasm");
        var aotFiles = FindSourceFiles(path, "*.aot");
        return Enumerable.Concat(wasmFiles, aotFiles).ToArray();
    }

    private string[] FindSourceFiles(string path, string searchPattern)
    {
        var files = Directory.GetFiles(path, searchPattern).ToArray();
        _logger.LogDebug("Found {Count} {Extension} file(s) in {Path}", files.Length, Path.GetExtension(searchPattern), path);
        return files;
    }
}
