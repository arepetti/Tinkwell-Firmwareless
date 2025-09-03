using Microsoft.Extensions.Logging;
using StreamJsonRpc;
using System.Diagnostics;
using System.IO.Pipes;
using Tinkwell.Firmwareless.WamrAotHost.Ipc.Requests;

namespace Tinkwell.Firmwareless.WamrAotHost.Ipc;

sealed class IpcClient(ILogger<IpcClient> logger, Settings settings) : IpcBase, IDisposable
{
    public Task StartClientAsync(string pipeName, string id, object clientCallbacks, CancellationToken cancellationToken)
    {
        _pipeName = pipeName;
        _id = id;
        _clientCallbacks = clientCallbacks;
        return StartClientImplAsync(cancellationToken);
    }

    public async Task DisconnectAsync()
    {
        _disconnecting = true;
        rpc?.Dispose();

        if (pipeClient is not null)
            await pipeClient.DisposeAsync();
    }

    void IDisposable.Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private readonly ILogger<IpcClient> _logger = logger;
    private readonly Settings _settings = settings;
    private JsonRpc? rpc;
    private NamedPipeClientStream? pipeClient;
    private string? _pipeName;
    private string? _id;
    private object? _clientCallbacks;
    private bool _disconnecting;
    private bool _disposed;

    private async Task StartClientImplAsync(CancellationToken cancellationToken)
    {
        Debug.Assert(_pipeName is not null);
        Debug.Assert(_id is not null);
        Debug.Assert(_clientCallbacks is not null);

        _disconnecting = false;
        for (int i=0; i < _settings.HostMaxConnectionAttempts; ++i)
        {
            try
            {
                _logger.LogDebug("Client: connecting to {PipeName} (attempt {Attempt} of {MaxAttempts})",
                    _pipeName, i + 1, _settings.HostMaxConnectionAttempts);

                pipeClient = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                pipeClient.Connect(_settings.HostConnectionTimeout);

                rpc = CreateJsonRpc(pipeClient, _clientCallbacks);
                rpc.Disconnected += OnDisconnected;
                rpc.StartListening();

                _logger.LogDebug("Registering the host {HostId}", _id);
                await rpc.NotifyAsync(CoordinatorMethods.RegisterClient, new RegisterClientRequest { ClientName = _id });
                return;
            }
            catch (IOException e)
            {
                _logger.LogError(e, "Connection failed: {Message}.Retrying...", e.Message);
                await Task.Delay(_settings.HostDelayBetweenAttemptsMs, cancellationToken);
            }
        }

        throw new HostException($"Host {_id} failed to connect to the coordination process.");
    }

    private void OnDisconnected(object? sender, EventArgs e)
    {
        if (_disconnecting)
            return;

        _logger.LogWarning("Host {HostId} disconnected from the server, reconnecting", _id);
        rpc?.Dispose();
        pipeClient?.Dispose();
        _ = StartClientImplAsync(CancellationToken.None); // Fire and forget reconnection
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _disconnecting = true;
            rpc?.Dispose();
            pipeClient?.Dispose();
        }

        _disposed = true;
    }
}
