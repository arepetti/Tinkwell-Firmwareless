using Docker.DotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System.Runtime.InteropServices;
using Tinkwell.Firmwareless.WasmHost;
using Tinkwell.Firmwareless.WasmHost.Packages;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole(options => options.FormatterName = nameof(ConsoleLogFormatter));
    logging.AddConsoleFormatter<ConsoleLogFormatter, ConsoleFormatterOptions>();
});

builder.ConfigureServices((services) =>
{
    services
        .AddOptions<Settings>()
        .BindConfiguration("Settings");
    
    services.AddHostedService<HostedService>();
    services.AddScoped<IPublicRepository, LocalDevelopmentFileSystemBasedRepository>();
    services.AddScoped<IPackageDiscovery, PackageDiscovery>();
    services.AddScoped<IPackageValidator, PackageValidator>();
    services.AddScoped<IContainerManager, ContainerManager>();

    services.AddSingleton<IDockerClient>(serviceProvider =>
    {
        var dockerUri = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "npipe://./pipe/docker_engine"
            : "unix:///var/run/docker.sock";
        return new DockerClientConfiguration(new System.Uri(dockerUri)).CreateClient();
    });
});

var host = builder.Build();
await host.RunAsync();
