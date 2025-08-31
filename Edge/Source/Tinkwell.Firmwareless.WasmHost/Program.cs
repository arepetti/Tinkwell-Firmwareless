using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tinkwell.Firmwareless.WasmHost;
using Tinkwell.Firmwareless.WasmHost.Packages;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices((services) =>
{
    services.AddHostedService<HostedService>();
    services.AddScoped<IPublicRepository, LocalDevelopmentFileSystemBasedRepository>();
    services.AddScoped<IPackageDiscovery, PackageDiscovery>();
    services.AddScoped<IPackageValidator, PackageValidator>();
});

var host = builder.Build();
await host.RunAsync();
