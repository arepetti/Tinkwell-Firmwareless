using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Firmwareless.WamrAotHost.Coordinator;

namespace Tinkwell.Firmwareless.WamrAotHost;

sealed record CoordinatorServiceOptions(string Path, string Parent, bool Transient);

sealed class CoordinatorService(ILogger<CoordinatorService> logger, HostProcessesCoordinator coordinator, CoordinatorServiceOptions options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string pipeName = IdHelpers.CreateId("tinkwell", 8);
        _logger.LogInformation("Parent URL: {Parent}", _options.Parent);
        _logger.LogInformation("Firmlets root path: {Path}", _options.Path);
        _logger.LogInformation("Coordinator pipe name: {PipeName}", pipeName);

        _logger.LogInformation("Starting firmlets...");
        _coordinator.Start(pipeName, FindFirmlets());

        if (!_options.Transient)
            await stoppingToken.WaitForCancellationAsync();
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
        return File.ReadAllLines(Path.Combine(_options.Path, "firmwares.txt"))
            .Where(line => !string.IsNullOrEmpty(line))
            .Select(line =>
            {
                var parts = line.Split('=', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return new FirmletEntry(parts[0], Path.Combine(_options.Path, parts[1].Trim('"')));
            });
    }
}
