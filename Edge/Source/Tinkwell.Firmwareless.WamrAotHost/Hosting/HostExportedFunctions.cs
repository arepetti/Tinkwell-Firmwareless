using Microsoft.Extensions.Logging;
using Tinkwell.Firmwareless.WamrAotHost.Coordinator.Mqtt;
using Tinkwell.Firmwareless.WamrAotHost.Ipc;
using Tinkwell.Firmwareless.WamrAotHost.Ipc.Requests;

namespace Tinkwell.Firmwareless.WamrAotHost.Hosting;

sealed class HostExportedFunctions(ILogger<HostExportedFunctions> logger, IpcClient ipcClient) : IHostExportedFunctions
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
                _logger.LogError("{Topic}: {Message}", topic, message);
                break;
            case 1:
                _logger.LogWarning("{Topic}: {Message}", topic, message);
                break;
            case 2:
                _logger.LogInformation("{Topic}: {Message}", topic, message);
                break;
            case 3:
                _logger.LogDebug("{Topic}: {Message}", topic, message);
                break;
            default:
                _logger.LogTrace("{Topic}: {Message}", topic, message);
                break;
        }
    }

    public void PublishMqttMessage(string topic, string payload)
    {
        _ipcClient.NotifyAsync(CoordinatorMethods.PublishMqttMessage, new MqttMessage(_ipcClient.HostId, topic, payload));
    }

    private readonly ILogger<HostExportedFunctions> _logger = logger;
    private readonly IpcClient _ipcClient = ipcClient;
}
