using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Tinkwell.Firmwareless.Vfs;
using Tinkwell.Firmwareless.WamrAotHost.Coordinator.Mqtt;
using Tinkwell.Firmwareless.WamrAotHost.Ipc;
using Tinkwell.Firmwareless.WamrAotHost.Ipc.Requests;

namespace Tinkwell.Firmwareless.WamrAotHost.Hosting;

sealed class HostExportedFunctions(ILogger<HostExportedFunctions> logger, IpcClient ipcClient, IVirtualFileSystem vfs) : IHostExportedFunctions
{
    public void Abort(string message, string fileName, int lineNumber, int columnNumber)
    {
        _logger.LogCritical("Fatal module error in {FileName} at {Line}:{Column}: {Message}", fileName, lineNumber, columnNumber, message);
        Environment.Exit(1);
    }

    public void Log(int severity, string topic, string message)
    {
        switch (severity)
        {
            case 0:
                _logger.LogError(ShortConsoleLogFormatter.FirmwareEntry, "{Topic}: {Message}", topic, message);
                break;
            case 1:
                _logger.LogWarning(ShortConsoleLogFormatter.FirmwareEntry, "{Topic}: {Message}", topic, message);
                break;
            case 2:
                _logger.LogInformation(ShortConsoleLogFormatter.FirmwareEntry, "{Topic}: {Message}", topic, message);
                break;
            case 3:
                _logger.LogDebug(ShortConsoleLogFormatter.FirmwareEntry, "{Topic}: {Message}", topic, message);
                break;
            default:
                _logger.LogTrace(ShortConsoleLogFormatter.FirmwareEntry, "{Topic}: {Message}", topic, message);
                break;
        }
    }

    public void PublishMqttMessage(string topic, string payload)
    {
        _ipcClient.NotifyAsync(CoordinatorMethods.PublishMqttMessage, new MqttMessage(_ipcClient.HostId, topic, payload))
            .GetAwaiter().GetResult();
    }

    public int OpenFile(string path, OpenMode mode, OpenFlags flags)
    {
        var file = _vfs.Open(Context.FromIdentity(_ipcClient.HostId), path, mode, flags);
        return _handles.Add(file);
    }

    public void CloseFile(int handle)
    {
        if (_handles.TryRemove(handle, out var file))
            file.Close(Context.FromIdentity(_ipcClient.HostId));
    }

    public int ReadFromFile(int handle, Span<byte> buffer, ReadFlags flags)
    {
        if (!_handles.TryGet(handle, out var file))
            throw new ArgumentException($"Invalid file handle {handle}");

        return file.Read(Context.FromIdentity(_ipcClient.HostId), buffer, flags);
    }

    public int WriteToFile(int handle, Span<byte> buffer, WriteFlags flags)
    {
        if (!_handles.TryGet(handle, out var file))
            throw new ArgumentException($"Invalid file handle {handle}");

        return file.Write(Context.FromIdentity(_ipcClient.HostId), buffer, flags);
    }

    private readonly ILogger<HostExportedFunctions> _logger = logger;
    private readonly IpcClient _ipcClient = ipcClient;
    private readonly IVirtualFileSystem _vfs = vfs;
    private readonly VfsFileHandleCollection _handles = new();
}

sealed class VfsFileHandleCollection
{
    public int Add(IVirtualFileSystemFile file)
    {
        var handle = Interlocked.Increment(ref _nextHandle);
        return _handles.TryAdd(handle, file) ? handle : 0;
    }

    public bool TryGet(int handle, [NotNullWhen(true)] out IVirtualFileSystemFile? file)
        => _handles.TryGetValue(handle, out file);

    public bool TryRemove(int handle, [NotNullWhen(true)] out IVirtualFileSystemFile? file)
        => _handles.TryRemove(handle, out file);

    private readonly ConcurrentDictionary<int, IVirtualFileSystemFile> _handles = new();
    private int _nextHandle = 0;
}