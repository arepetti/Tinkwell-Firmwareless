using Azure.Storage.Blobs;
using Docker.DotNet;
using System.Runtime.InteropServices;
using Tinkwell.Firmwareless.CompilationServer.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

builder.Services.AddSingleton(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("tinkwell-firmwarestore")
             ?? throw new InvalidOperationException("Missing connection string 'tinkwell-firmwarestore'.");
    return new BlobContainerClient(connectionString, "tinkwell-firmwarestore-builds");
});

builder.Services.AddSingleton<IDockerClient>(serviceProvider => 
{
    var dockerUri = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? "npipe://./pipe/docker_engine" 
        : "unix:///var/run/docker.sock";
    return new DockerClientConfiguration(new Uri(dockerUri)).CreateClient();
});

builder.Services.AddScoped<ICompilationService, CompilationService>();

var app = builder.Build();
//app.UseHttpsRedirection();
app.MapControllers();
app.Run();