using Azure.Identity;
using Azure.Security.KeyVault.Keys.Cryptography;
using Docker.DotNet;
using System.Runtime.InteropServices;
using Tinkwell.Firmwareless;
using Tinkwell.Firmwareless.CompilationServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddDefaultLogging();
builder.AddInvalidModelStateLogging();

// External resourcess
builder.Services.AddAspireBlobContainerClientFactory();

// Services.
// Azure Key Vault is configured only for production
if (builder.Environment.IsProduction())
{
    var keyName = builder.Configuration["KeyVault:SigningKeyName"];
    if (string.IsNullOrEmpty(keyName))
        throw new InvalidOperationException("The 'KeyVault:SigningKeyName' must be configured with the name of the KeyVault key.");
   
    var keyVaultUri = builder.Configuration["AzureKeyVaults:keyvault:VaultUri"]!;
    var keyId = new Uri(new Uri(keyVaultUri), $"/keys/{keyName}");
   
    // Register the CryptographyClient as before, but now it's powered by Aspire's configuration
   builder.Services.AddSingleton(sp =>
    new CryptographyClient(keyId, new DefaultAzureCredential()));
   builder.Services.AddScoped<IKeyVaultSignatureService, KeyVaultSignatureService>();
}
else
{
    builder.Services.AddScoped<IKeyVaultSignatureService, LocalDevelopmentSignatureService>();
}

builder.Services.AddSingleton<IDockerClient>(serviceProvider => 
{
    var dockerUri = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? "npipe://./pipe/docker_engine" 
        : "unix:///var/run/docker.sock";
    return new DockerClientConfiguration(new System.Uri(dockerUri)).CreateClient();
});

builder.Services.AddScoped<Compiler>();
builder.Services.AddScoped<ICompilationService, CompilationService>();
builder.Services.AddScoped<CompiledFirmwarePackage>();

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

public partial class Program { }
