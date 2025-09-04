using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Tinkwell.Firmwareless.WamrAotHost;
using Tinkwell.Firmwareless.WamrAotHost.Coordinator;
using Tinkwell.Firmwareless.WamrAotHost.Hosting;
using Tinkwell.Firmwareless.WamrAotHost.Ipc;

var stopwatch = new Stopwatch();
stopwatch.Start();

var cli = new CommandLineParser(args);
var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole(o => o.FormatterName = nameof(ShortConsoleLogFormatter));
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
            .AddSingleton(cli.GetCoordinatorServiceOptions())
            .AddSingleton<IpcServer>()
            .AddSingleton<SystemResourcesUsageArbiter>()
            .AddSingleton<HostProcessesCoordinator>()
            .AddHostedService<CoordinatorService>();
    }
});

using var host = builder.Build();
await host.StartAsync();

stopwatch.Stop();
host.Services.GetRequiredService<ILogger<Program>>().LogInformation("Execution time for {Name} is: {Time} ms",
    cli.Name, stopwatch.ElapsedMilliseconds);

return 0;
