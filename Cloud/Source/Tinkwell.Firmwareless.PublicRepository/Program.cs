using Azure.Identity;
using Azure.Security.KeyVault.Keys.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Tinkwell.Firmwareless;
using Tinkwell.Firmwareless.Cloud.Security;
using Tinkwell.Firmwareless.PublicRepository.Authentication;
using Tinkwell.Firmwareless.PublicRepository.Configuration;
using Tinkwell.Firmwareless.PublicRepository.Database;
using Tinkwell.Firmwareless.PublicRepository.Middleware;
using Tinkwell.Firmwareless.PublicRepository.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddDefaultLogging();
builder.AddInvalidModelStateLogging();

// Azure Key Vault is configured only for production
if (builder.Environment.IsProduction())
{
    var keyName = builder.Configuration["KeyVault:SigningKeyName"];
    if (string.IsNullOrEmpty(keyName))
        throw new InvalidOperationException("The 'KeyVault:SigningKeyName' must be configured with the name of the KeyVault key.");

    var keyVaultUri = builder.Configuration["AzureKeyVaults:keyvault:VaultUri"]!;
    var keyId = new Uri(new Uri(keyVaultUri), $"/keys/{keyName}");

    builder.Services.AddSingleton(sp => new CryptographyClient(keyId, new DefaultAzureCredential()));
    builder.Services.AddScoped<IKeyVaultSignatureService, KeyVaultSignatureService>();
}
else
{
    builder.Services.AddScoped<IKeyVaultSignatureService, LocalDevelopmentSignatureService>();
}

// Features
builder.Services.Configure<FileUploadOptions>(builder.Configuration.GetSection("FileUploads"));
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

// External resourcess
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<AppDbContext>(x =>
        x.UseNpgsql(builder.Configuration.GetConnectionString("tinkwell-firmwaredb-manifests")));

    builder.Services.AddAspireBlobContainerClient("tinkwell-firmwarestore-assets");
}

builder.Services.AddServiceDiscovery();
builder.Services.AddAspireHttpClient("tinkwell-compilation-server");

// Authentication
builder.Services.AddAuthentication(ApiKeyAuthHandler.Scheme)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthHandler>(ApiKeyAuthHandler.Scheme, _ => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", p => p.RequireRole("Admin"));
});

// Services
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

// Build the app
var app = builder.Build();

app.UseMiddleware<ProblemDetailsMiddleware>();
app.AddFailedResponseLogging();

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