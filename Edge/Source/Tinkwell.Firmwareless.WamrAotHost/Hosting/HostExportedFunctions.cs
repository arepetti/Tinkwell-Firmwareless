using Microsoft.Extensions.Logging;

namespace Tinkwell.Firmwareless.WamrAotHost.Hosting;

sealed class HostExportedFunctions(ILogger<HostExportedFunctions> logger) : IHostExportedFunctions
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
        _logger.LogTrace("Publishing MQTT message in {Topic}: {Payload}", topic, payload);
    }

    private readonly ILogger<HostExportedFunctions> _logger = logger;
}
