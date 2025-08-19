using Docker.DotNet;
using System.Runtime.InteropServices;
using Tinkkwell.Firmwareless;
using Tinkwell.Firmwareless.CompilationServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddDefaultLogging();
builder.AddInvalidModelStateLogging();

// External resourcess
builder.Services.AddAspireBlobContainerClientFactory();

builder.Services.AddSingleton<IDockerClient>(serviceProvider => 
{
    var dockerUri = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? "npipe://./pipe/docker_engine" 
        : "unix:///var/run/docker.sock";
    return new DockerClientConfiguration(new Uri(dockerUri)).CreateClient();
});

// Services
builder.Services.AddScoped<Compiler>();
builder.Services.AddScoped<ICompilationService, CompilationService>();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        foreach (var c in JsonDefaults.Options.Converters)
            options.JsonSerializerOptions.Converters.Add(c);
    });


// Build the app
var app = builder.Build();

app.AddExceptionLogging();
app.AddFailedResponseLogging();

app.UseHttpsRedirection();
app.UseRouting();
app.MapControllers();
app.Run();