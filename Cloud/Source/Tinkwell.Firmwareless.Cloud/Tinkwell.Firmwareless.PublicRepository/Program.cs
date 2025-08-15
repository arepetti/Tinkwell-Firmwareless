using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using Tinkwell.Firmwareless.PublicRepository.Authentication;
using Tinkwell.Firmwareless.PublicRepository.Configuration;
using Tinkwell.Firmwareless.PublicRepository.Controllers;
using Tinkwell.Firmwareless.PublicRepository.Database;
using Tinkwell.Firmwareless.PublicRepository.Repositories;
using Tinkwell.Firmwareless.PublicRepository.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Azure Key Vault is configured only for production, when developing locally we use environment variables
if (builder.Environment.IsProduction())
{
    var vaultUri = builder.Configuration["KeyVault:VaultUri"];
    if (!string.IsNullOrWhiteSpace(vaultUri))
        builder.Configuration.AddAzureKeyVault(new Uri(vaultUri), new DefaultAzureCredential());
}

builder.Services.Configure<ApiKeyOptions>(builder.Configuration.GetSection("ApiKeys"));
builder.Services.PostConfigure<ApiKeyOptions>(opt =>
{
    if (string.IsNullOrWhiteSpace(opt.HmacSecret))
        throw new InvalidOperationException("ApiKeys:HmacSecret is not configured.");

    if (opt.HmacBytes is < 4 or > 32)
        throw new InvalidOperationException("ApiKeys:HmacBytes must be between 4 and 32.");

    if (string.IsNullOrWhiteSpace(opt.KeyPrefix))
        opt.KeyPrefix = "ak_";
});

builder.Services.Configure<FileUploadOptions>(builder.Configuration.GetSection("FileUploads"));

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<AppDbContext>(x =>
        x.UseNpgsql(builder.Configuration.GetConnectionString("tinkwell-firmwaredb-manifests")));

    builder.Services.AddSingleton(serviceProvider =>
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("tinkwell-firmwarestore-assets")
                 ?? throw new InvalidOperationException("Missing connection string 'tinkwell-firmwarestore-assets'.");
        return new BlobContainerClient(connectionString, "tinkwell-firmwarestore-assets");
    });
}

builder.Services.AddAuthentication(ApiKeyAuthHandler.Scheme)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthHandler>(ApiKeyAuthHandler.Scheme, _ => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", p => p.RequireRole("Admin"));
});

builder.Services.AddHttpClient("tinkwell-compilation-server");
builder.Services.AddScoped<IApiKeyValidator, ApiKeyValidationService>();
builder.Services.AddScoped<KeysService>();
builder.Services.AddScoped<VendorsService>();
builder.Services.AddScoped<ProductsService>();
builder.Services.AddScoped<FirmwaresService>();
builder.Services.AddScoped<CompilationProxyService>();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        foreach (var c in JsonDefaults.Options.Converters)
            options.JsonSerializerOptions.Converters.Add(c);
    });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await app.ApplyMigrationsAsync(scope);
    await app.AddProvisioningKeys(scope);
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();