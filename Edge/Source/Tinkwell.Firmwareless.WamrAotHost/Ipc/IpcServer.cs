using Microsoft.Extensions.Logging;
using StreamJsonRpc;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;

namespace Tinkwell.Firmwareless.WamrAotHost.Ipc;

sealed class IpcServer(ILogger<IpcServer> logger, Settings settings) : IpcBase, IDisposable
{
    public async Task StartAsync(string pipeName, object serverCallbacks)
    {
        _pipeName = pipeName ?? throw new ArgumentNullException(nameof(pipeName));
        _serverCallbacks = serverCallbacks ?? throw new ArgumentNullException(nameof(serverCallbacks));

        _logger.LogDebug("Server listening on pipe {PipeName}...", _pipeName);
        while (!_cts.IsCancellationRequested)
        {
            var pipeServer = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.InOut,
                _settings.CoordinatorMaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            await pipeServer.WaitForConnectionAsync(_cts.Token);

            _ = Task.Run(() => HandleClientAsync(pipeServer), _cts.Token);
        }
    }

    public Task NotifyAsync(string hostId, string notificationName)
    {
        if (_clients.TryGetValue(hostId, out var rpc))
            return rpc.NotifyAsync(notificationName);

        _logger.LogWarning("Cannot find host {HostId} to send notification '{Notification}'", hostId, notificationName);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _cts.Cancel();
        foreach (var rpc in _clients.Values)
            rpc.Dispose();
        _cts.Dispose();
    }

    private readonly ILogger<IpcServer> _logger = logger;
    private readonly Settings _settings = settings;
    private string? _pipeName;
    private object? _serverCallbacks;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<string, JsonRpc> _clients = new();

    private async Task HandleClientAsync(NamedPipeServerStream pipe)
    {
        Debug.Assert(_serverCallbacks is not null);

        _logger.LogDebug("Client connected");

        var rpc = CreateJsonRpc(pipe, _serverCallbacks);
        rpc.Disconnected += (s, e) =>
        {
            if (e.Reason != DisconnectedReason.RemotePartyTerminated)
                _logger.LogWarning("Client disconnected. {Reason}: {Message}", e.Reason, e.Exception?.Message);

            _clients.TryRemove(pipe.GetHashCode().ToString(), out _);
            rpc.Dispose();
            pipe.Dispose();
        };

        _clients[pipe.GetHashCode().ToString()] = rpc;
        rpc.StartListening();

        try
        {
            // Keep waiting and server all the client's requests
            await Task.Delay(Timeout.Infinite, _cts.Token);
        }
        catch (TaskCanceledException) { }
    }
}
