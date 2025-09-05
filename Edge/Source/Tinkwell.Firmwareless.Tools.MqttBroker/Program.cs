using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Firmwareless.Tools.MqttBroker;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureLogging(logging =>
{
    logging.ClearProviders();
    logging.AddSimpleConsole();
});

builder.ConfigureServices((context, services) =>
{
    services.AddHostedService<MqttBroker>();
});

using var host = builder.Build();
await host.StartAsync();
await host.WaitForShutdownAsync();
