using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using Tinkwell.Firmwareless.WamrAotHost.Hosting;

var stopwatch = new Stopwatch();
stopwatch.Start();

using var host = Host.CreateDefaultBuilder([])
    .ConfigureLogging(builder =>
    {
        builder.ClearProviders();
        builder.AddSimpleConsole(configure =>
        {
            configure.SingleLine = true;
        });
    })
    .ConfigureServices(services =>
    {
        services.AddSingleton<HostService>();
        services.AddSingleton<IModuleLoader, ModuleLoader>();
        services.AddSingleton<IHostExportedFunctions, HostExportedFunctions>();
        services.AddSingleton<IRegisterHostUnsafeNativeFunctions, HostExportedUnsafeNativeFunctions>();
    })
    .Build();

var hostCommandPathArg = new Argument<string>("path").AcceptLegalFilePathsOnly();
var hostCommandTransientOption = new Option<bool>("--transient");
var hostCommand = new Command("host")
{
    hostCommandPathArg,
    hostCommandTransientOption
};

hostCommand.SetAction(pr =>
{
    var service = host.Services.GetRequiredService<HostService>();
    service.Start(pr.GetRequiredValue(hostCommandPathArg), pr.GetValue(hostCommandTransientOption));
});

var brokerCommandPathArg = new Argument<string>("path").AcceptLegalFilePathsOnly();
var brokerCommandParentUrlArg = new Argument<string>("parent");
var brokerCommand = new Command("broker")
{
    brokerCommandPathArg,
    brokerCommandParentUrlArg
};

brokerCommand.SetAction(_ =>
{
    host.Services.GetRequiredService<ILogger<Program>>()
        .LogError("Not implemented");
    return 1;
});

var parser = CommandLineParser.Parse(new RootCommand { brokerCommand, hostCommand }, args);
try
{
    return await parser.InvokeAsync();
}
finally
{
    stopwatch.Stop();
    host.Services.GetRequiredService<ILogger<Program>>()
        .LogInformation("Execution time: {Time} ms", stopwatch.ElapsedMilliseconds);
}