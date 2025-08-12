using Azure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Tinkwell.Firmwareless.PublicRepository.Authentication;
using Tinkwell.Firmwareless.PublicRepository.Database;
using Tinkwell.Firmwareless.PublicRepository.Repositories;

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
        opt.KeyPrefix = "pr_";
});

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("tinkwell-firmwaredb-manifests")));

builder.Services.AddAuthentication(ApiKeyAuthHandler.Scheme)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthHandler>(ApiKeyAuthHandler.Scheme, _ => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", p => p.RequireRole("Admin"));
    options.AddPolicy("Publisher", p => p.RequireClaim("scope", "firmware.write"));
});

builder.Services.AddScoped<KeyService>();

builder.Services.AddControllers();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await app.ApplyMigrationsAsync(scope);

    // Bootstrap Admin key, no expiration but temporary! It should be replaced with a proper key
    // as soon as possible and then revoked. To use only for bootstrapping and for local testing.
    var keyService = scope.ServiceProvider.GetRequiredService<KeyService>();
    if (await keyService.HasAdminKeyAsync() == false)
    {
        var adminKey = await keyService.UnsafeCreateForAdminAsync();
        Console.WriteLine($"[BOOTSTRAP] Admin API key (store securely): {adminKey}");
    }
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();