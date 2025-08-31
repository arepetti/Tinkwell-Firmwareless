using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;
using System.CommandLine.Parsing;
using Tinkwell.Firmwareless.WamrAotHost.Hosting;

using var host = Host.CreateDefaultBuilder([])
    .ConfigureServices(services =>
    {
        services.AddSingleton<HostService>();
        services.AddSingleton<IModuleLoader, ModuleLoader>();
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
    Console.WriteLine("Not implemented");
    return 1;
});

var parser = CommandLineParser.Parse(new RootCommand { brokerCommand, hostCommand }, args);
return await parser.InvokeAsync();
