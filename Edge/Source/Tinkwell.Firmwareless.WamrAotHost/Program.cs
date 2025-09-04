using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Tinkwell.Firmwareless.WamrAotHost;
using Tinkwell.Firmwareless.WamrAotHost.Coordinator;
using Tinkwell.Firmwareless.WamrAotHost.Coordinator.Monitoring;
using Tinkwell.Firmwareless.WamrAotHost.Coordinator.Mqtt;
using Tinkwell.Firmwareless.WamrAotHost.Hosting;
using Tinkwell.Firmwareless.WamrAotHost.Ipc;

var cli = new CommandLineParser(args);
var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole(options => options.FormatterName = nameof(ShortConsoleLogFormatter));
    logging.AddConsoleFormatter<ShortConsoleLogFormatter, ConsoleFormatterOptions>();
});

builder.ConfigureServices((context, services) =>
{
    services
        .AddOptions<Settings>()
        .BindConfiguration("Settings");

    services.AddSingleton(x => x.GetRequiredService<IOptions<Settings>>().Value);

    if (cli.RequiredService == RequiredService.Host)
    {
        services
           .AddSingleton<IWamrHost, WamrHost>()
           .AddSingleton<IHostExportedFunctions, HostExportedFunctions>()
           .AddSingleton<IRegisterHostUnsafeNativeFunctions, HostExportedUnsafeNativeFunctions>()
           .AddSingleton(cli.GetHostServiceOptions())
           .AddSingleton<IpcClient>()
           .AddHostedService<HostService>();
    }
    else
    {
        services
            .AddSingleton<MqttQueue, MqttQueue>()
            .AddHostedService<MqttMessagesProcessingService>()
            .AddSingleton(cli.GetCoordinatorServiceOptions())
            .AddSingleton<IpcServer>()
            .AddSingleton<SystemResourcesUsageArbiter>()
            .AddSingleton<FirmletsRepository>()
            .AddSingleton<CoordinatorRpc>()
            .AddSingleton<HostProcessesCoordinator>()
            .AddHostedService<CoordinatorService>();
    }
});

using var host = builder.Build();
await host.StartAsync();

