using Microsoft.Extensions.Logging;
using StreamJsonRpc;
using System.Collections.Concurrent;
using System.Diagnostics;
using Tinkwell.Firmwareless.WamrAotHost.Ipc;
using Tinkwell.Firmwareless.WamrAotHost.Ipc.Requests;

namespace Tinkwell.Firmwareless.WamrAotHost.Coordinator;

sealed class HostProcessesCoordinator(ILogger<HostProcessesCoordinator> logger, IpcServer server)
{
    public void Start(string pipeName, IEnumerable<string> paths)
    {
        _pipeName = pipeName;
        _ = _server.StartAsync(_pipeName, this); // Fire and forget

        _hosts = new ConcurrentDictionary<string, HostInfo>(
            paths
                .Select(path => new HostInfo(IdHelpers.CreateId("firmlet", 8), path))
                .ToDictionary(x => x.Id, x => x)
        );

        foreach (var hostInfo in _hosts.Values)
            StartHostProcess(hostInfo, _pipeName);
    }

    [JsonRpcMethod(nameof(RegisterClient))]
    public void RegisterClient(RegisterClientRequest request)
    {
        Debug.Assert(_hosts is not null);

        _logger.LogDebug("Host {HostId} is ready", request.ClientName);
        if (_hosts.TryGetValue(request.ClientName, out var host))
            host.Ready = true;
    }

    private sealed record HostInfo(string Id, string Path)
    {
        public bool Ready { get; set; }
        public Process? Process { get; set; }
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public int RestartCount { get; set; }
    }

    private readonly ILogger<HostProcessesCoordinator> _logger = logger;
    private ConcurrentDictionary<string, HostInfo>? _hosts;
    private readonly IpcServer _server = server;
    private string? _pipeName;

    private void StartHostProcess(HostInfo hostInfo, string pipeName)
    {
        _logger.LogDebug("Starting host {HostId} ({Path}).", hostInfo.Id, hostInfo.Path);
        var process = SpawnWithArgs([
            "host",
            $"--path={hostInfo.Path}",
            $"--id={hostInfo.Id}",
            $"--pipe-name={pipeName}",
        ]);

        if (process is null)
        {
            _logger.LogError("Failed to start {HostId} ({Path}).", hostInfo.Id, hostInfo.Path);
            return;
        }

        // This is a bit of a race condition: if we're slow enough (unlikely...) and the startup fails
        // then the process could exit before we started monitoring.
        if (process.HasExited)
        {
            _logger.LogWarning("Host process for {HostId} exited immediately with code {ExitCode}. Restarting.", hostInfo.Id, process.ExitCode);
            _ = RestartProcessWithBackoffAsync(hostInfo);
            return;
        }

        process.EnableRaisingEvents = true;
        process.Exited += OnProcessExited;

        hostInfo.Process = process;
        _logger.LogDebug("Started {HostId} for {Path}, PID {PID}", hostInfo.Id, hostInfo.Path, process.Id);
    }

    private async void OnProcessExited(object? sender, EventArgs e)
    {
        var process = (Process)sender!;

        var hostInfo = _hosts?.Values.FirstOrDefault(h => h.Process?.Id == process.Id);
        if (hostInfo is null)
            return;

        _logger.LogWarning("Host {HostId} (PID {PID}) exited with code: {ExitCode}. Restarting.", hostInfo.Id, process.Id, process.ExitCode);

        await RestartProcessWithBackoffAsync(hostInfo);
    }

    private async Task RestartProcessWithBackoffAsync(HostInfo hostInfo)
    {
        Debug.Assert(_pipeName is not null);

        hostInfo.RestartCount++;
        var delaySeconds = Math.Min(60, Math.Pow(2, hostInfo.RestartCount));

        _logger.LogTrace("Waiting {Delay} seconds before restarting {HostId}", delaySeconds, hostInfo.Id);
        await Task.Delay(TimeSpan.FromSeconds(delaySeconds));

        _logger.LogInformation("Restarting host {HostId} (Attempt #{RestartCount})...", hostInfo.Id, hostInfo.RestartCount);
        StartHostProcess(hostInfo, _pipeName);
    }

    private static Process? SpawnWithArgs(string[] newArgs)
    {
        if (Environment.ProcessPath is null)
            throw new InvalidOperationException("Cannot determine process' path");

        bool launchedViaDotnet = Path.GetFileNameWithoutExtension(Environment.ProcessPath).Equals("dotnet");

        string launchTarget;
        string arguments = string.Join(' ', newArgs.Select(x => $"\"{x}\""));

        if (launchedViaDotnet)
        {
            launchTarget = Environment.ProcessPath;
            arguments = $"\"{Environment.GetCommandLineArgs()[0]}\"" + " " + arguments;
        }
        else
        {
            launchTarget = Environment.ProcessPath;
        }

        return Process.Start(new ProcessStartInfo
        {
            FileName = launchTarget,
            Arguments = arguments,
            UseShellExecute = false
        });
    }
}
