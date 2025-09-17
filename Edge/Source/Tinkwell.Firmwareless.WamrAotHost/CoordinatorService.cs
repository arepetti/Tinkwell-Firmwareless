using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Firmwareless.WamrAotHost.Coordinator;

namespace Tinkwell.Firmwareless.WamrAotHost;

sealed record CoordinatorServiceOptions(string Path, string MqttBrokerAddress, int MqttBrokerPort, string MqttClientId, string MqttTopicFilter, bool Transient);

sealed class CoordinatorService(ILogger<CoordinatorService> logger, HostProcessesCoordinator coordinator, CoordinatorServiceOptions options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string pipeName = IdHelpers.CreateId("tinkwell", 8);
        _logger.LogInformation("MQTT broker: {Address}:{Port}", _options.MqttBrokerAddress, _options.MqttBrokerPort);
        _logger.LogInformation("Firmlets root path: {Path}", _options.Path);
        _logger.LogInformation("Coordinator pipe name: {PipeName}", pipeName);

        _logger.LogInformation("Starting Coordinator...");
        _coordinator.Start(pipeName, FindFirmlets());

        if (!_options.Transient)
            await stoppingToken.WaitCancellation();

        _logger.LogInformation("Stopping coordinator...");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        _logger.LogInformation("Stopping firmlets...");
        await _coordinator.StopAsync();
    }

    private readonly ILogger<CoordinatorService> _logger = logger;
    private readonly CoordinatorServiceOptions _options = options;
    private readonly HostProcessesCoordinator _coordinator = coordinator;

    private IEnumerable<FirmletEntry> FindFirmlets()
    {
        return File.ReadAllLines(Path.Combine(_options.Path, "firmlets"))
            .Where(line => !string.IsNullOrEmpty(line))
            .Select(line =>
            {
                var parts = line.Split('=', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return new FirmletEntry(parts[0], Path.Combine(_options.Path, parts[1].Trim('"')));
            });
    }
}
