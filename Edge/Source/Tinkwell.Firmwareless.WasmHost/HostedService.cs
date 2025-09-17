using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Firmwareless.WasmHost.Packages;
using Tinkwell.Firmwareless.WasmHost.Runtime;

namespace Tinkwell.Firmwareless.WasmHost;

sealed class HostedService(ILogger<HostedService> logger, IFirmletsManager firmletsManager, IContainerManager containerManager) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting...");
        await _firmletsManager.StartAsync(cancellationToken);
        await _containerManager.StartAsync(cancellationToken);
        _logger.LogInformation("Started");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _containerManager.StopAsync(cancellationToken);
        _logger.LogInformation("Stopped");
    }

    private readonly ILogger<HostedService> _logger = logger;
    private readonly IFirmletsManager _firmletsManager = firmletsManager;
    private readonly IContainerManager _containerManager = containerManager;
}
