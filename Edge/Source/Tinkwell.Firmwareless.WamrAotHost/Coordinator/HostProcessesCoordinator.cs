using Microsoft.Extensions.Logging;
using StreamJsonRpc;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Tinkwell.Firmwareless.WamrAotHost.Ipc;
using Tinkwell.Firmwareless.WamrAotHost.Ipc.Requests;

namespace Tinkwell.Firmwareless.WamrAotHost.Coordinator;

sealed record FirmletEntry(string Id, string Path);

sealed class HostProcessesCoordinator(ILogger<HostProcessesCoordinator> logger, Settings settings, IpcServer server, SystemResourcesUsageArbiter arbiter)
    : IDisposable
{
    [DynamicDependency(nameof(RegisterClient), typeof(HostProcessesCoordinator))]
    public void Start(string pipeName, IEnumerable<FirmletEntry> entries)
    {
        _pipeName = pipeName;
        _ = _server.StartAsync(_pipeName, this); // Fire and forget

        _hosts = new ConcurrentDictionary<string, HostInfo>(
            entries
                .Select(entry => new HostInfo(entry.Id, IdHelpers.CreateId("firmlet", 12), entry.Path))
                .ToDictionary(x => x.Id, x => x)
        );

        foreach (var hostInfo in _hosts.Values)
            StartHostProcess(hostInfo, _pipeName);

        _monitorTimer = new Timer(
            callback: MonitorProcesses,
            state: null,
            dueTime: TimeSpan.FromMilliseconds(_settings.CoordinatorStartProcessTimeoutMs + 100),
            period: TimeSpan.FromMilliseconds(_settings.CoordinatorMonitoringIntervalMs));
    }

    public async Task StopAsync()
    {
        if (_hosts is null)
            return;

        _keepAlive = false;

        foreach (var host in _hosts.Values.Where(x => x.Process is not null && !x.Process.HasExited))
            GentlyTerminateProcess(host, "shutting down");

        await Task.Delay(_settings.CoordinatorShutdownProcessTimeoutMs);

        foreach (var host in _hosts.Values.Where(x => x.Process is not null && !x.Process.HasExited))
            ForcefullyTerminateProcess(host);
    }

    [JsonRpcMethod(CoordinatorMethods.RegisterClient)]
    public void RegisterClient(RegisterClientRequest request)
    {
        Debug.Assert(_hosts is not null);

        if (_hosts.TryGetValue(request.ClientName, out var host))
        {
            _logger.LogInformation("Host {HostId} is ready ({Time} ms)",
                request.ClientName, (DateTime.UtcNow - host.StartTime).TotalMilliseconds);

            host.Ready = true;
        }
    }

    public void Dispose()
        => _monitorTimer?.Dispose();

    private readonly ILogger<HostProcessesCoordinator> _logger = logger;
    private readonly Settings _settings = settings;
    private readonly IpcServer _server = server;
    private readonly SystemResourcesUsageArbiter _arbiter = arbiter;
    private ConcurrentDictionary<string, HostInfo>? _hosts;
    private string? _pipeName;
    private bool _keepAlive = true;
    private Timer? _monitorTimer;

    private void StartHostProcess(HostInfo hostInfo, string pipeName)
    {
        _logger.LogDebug("Starting host {HostId} ({Path}).", hostInfo.Id, hostInfo.Path);

        hostInfo.Ready = false;
        hostInfo.Terminating = false;
        hostInfo.UsageData.Clear();

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

        hostInfo.Process = process;
        hostInfo.StartTime = DateTime.UtcNow;

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

        _logger.LogDebug("Started {HostId} for {Path}, PID {PID}", hostInfo.Id, hostInfo.Path, process.Id);
    }

    private async void OnProcessExited(object? sender, EventArgs e)
    {
        var process = (Process)sender!;

        var hostInfo = _hosts?.Values.FirstOrDefault(h => h.Process?.Id == process.Id);
        if (hostInfo is null)
            return;

        _logger.LogWarning("Host {HostId} (PID {PID}) exited with code: {ExitCode}.", hostInfo.Id, process.Id, process.ExitCode);

        if (_keepAlive)
            await RestartProcessWithBackoffAsync(hostInfo);
    }

    private async Task RestartProcessWithBackoffAsync(HostInfo hostInfo)
    {
        Debug.Assert(_pipeName is not null);

        var now = DateTime.UtcNow;
        var timeWindow = TimeSpan.FromHours(1);
        var limit = now - timeWindow;
        hostInfo.RestartTimestamps.RemoveAll(t => t < limit);

        if (_settings.CoordinatorMaxHostRestartsPerHour != -1 && hostInfo.RestartTimestamps.Count >= _settings.CoordinatorMaxHostRestartsPerHour)
        {
            _logger.LogCritical(
                "Host {HostId} has failed {Count} times in the last {Window}. It will not be restarted again.",
                hostInfo.Id,
                hostInfo.RestartTimestamps.Count,
                timeWindow);

            // Mark the host as terminating to prevent any further actions
            hostInfo.Terminating = true;
            return;
        }

        hostInfo.RestartTimestamps.Add(now);
        var recentRestartCount = hostInfo.RestartTimestamps.Count;
        var delaySeconds = CalculateDelay();

        _logger.LogTrace("Waiting {Delay} seconds before restarting {HostId}", delaySeconds, hostInfo.Id);
        await Task.Delay(TimeSpan.FromSeconds(delaySeconds));

        _logger.LogInformation(
            "Restarting host {HostId} (Attempt #{Count} in the last {Window})...",
            hostInfo.Id,
            recentRestartCount,
            timeWindow);

        StartHostProcess(hostInfo, _pipeName);

        double CalculateDelay()
        {
            const double cap = 60;
            const double k = 0.9;
            const double midpoint = 4;

            return Math.Clamp(Math.Round(cap / (1 + Math.Exp(-k * (recentRestartCount - midpoint))), 1), 1, cap);
        }
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

    private async void MonitorProcesses(object? state)
    {
        try
        {
            if (_hosts is null || _hosts.IsEmpty)
                return;

            _logger.LogTrace("Running periodic check on {Count} host processes.", _hosts.Count);

            var now = DateTime.UtcNow;
            var startProcessTimeout = TimeSpan.FromMilliseconds(_settings.CoordinatorStartProcessTimeoutMs);

            foreach (var host in _hosts.Values)
            {
                if (host.Process is null || host.Process.HasExited)
                    continue;

                // It's been asked to quit but it's still here, kill it
                if (host.Terminating)
                {
                    ForcefullyTerminateProcess(host);
                    continue;
                }

                // Too much time to bootstrap? Kill it and retry
                if (!host.Ready && now - host.StartTime > startProcessTimeout)
                {
                    GentlyTerminateProcess(host, "bootstrapping timeout");
                    continue;
                }
            }

            await ResourcesUsageMeter.CollectAsync(_hosts.Values);
            foreach (var host in _hosts.Values)
                _logger.LogTrace("Host status - {HostInfo}", host.ToString());

            foreach (var decision in _arbiter.Assess(_hosts.Values))
            {
                switch (decision.Decision)
                {
                    case SystemResourcesUsageArbiter.Decision.None:
                        continue;
                    case SystemResourcesUsageArbiter.Decision.Terminate:
                        GentlyTerminateProcess(decision.HostInfo, "exceeding its rations");
                        break;
                    case SystemResourcesUsageArbiter.Decision.Suspend:
                        throw new NotImplementedException();
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error occurred during the periodic process check: {Message}", e.Message);
        }
    }

    private void GentlyTerminateProcess(HostInfo hostInfo, string reason)
    {
        // Died already on its own?
        if (hostInfo.Process is null || hostInfo.Process.HasExited)
            return;

        _logger.LogWarning("Marking {HostId} for termination: {Reason}", hostInfo.Id, reason);
        hostInfo.Terminating = true;
        _server.NotifyAsync(hostInfo.Id, HostMethods.Shutdown);
    }

    private void ForcefullyTerminateProcess(HostInfo hostInfo, string reason = "not keen on goodbyes")
    {
        // Died already on its own?
        if (hostInfo.Process is null || hostInfo.Process.HasExited)
            return;

        _logger.LogInformation("Forcefully terminating {HostId}: {Reason}", hostInfo.Id, reason);

        // Simply kill the process. The OnProcessExited event will fire,
        // triggering the existing restart logic with its backoff policy.
        // We ignore the errors, if something went wrong then we'll simply try again next time.
        try
        {
            hostInfo.Process?.Kill(true);
        }
        catch (InvalidOperationException)
        {
        }
        catch (NotSupportedException)
        {
        }
        catch (AggregateException)
        {
        }
    }
}
