using Azure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tinkwell.Firmwareless.PublicRepository.Authentication;
using Tinkwell.Firmwareless.PublicRepository.Database;

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

builder.Services.AddControllers();

var app = builder.Build();

// DB migrate + bootstrap Admin key (no expiration)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var apiOpts = scope.ServiceProvider.GetRequiredService<IOptions<ApiKeyOptions>>().Value;

    const int maxRetries = 5;
    const int delayBetweenRetries = 2000;
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {

            app.Logger.LogInformation("Attempt {Attempt} to migrate database...", attempt);
            db.Database.Migrate();
            app.Logger.LogInformation("Migration successful");
            break;
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Migration failed: {Reason}", ex.Message);

            if (attempt == maxRetries)
            {
                app.Logger.LogInformation("Max retries reached. Giving up.");
                throw;
            }

            await Task.Delay(delayBetweenRetries);
        }
    }

    // Bootstrap Admin key (no expiration)
    if (!db.ApiKeys.Any(k => k.Role == "Admin" && k.RevokedAt == null))
    {
        var adminId = Guid.NewGuid();
        var adminKey = ApiKeyFormat.Generate(adminId, apiOpts);
        Console.WriteLine($"[BOOTSTRAP] Admin API key (store securely): {adminKey}");

        var (hash, salt) = ApiKeyHasher.Hash(adminKey);

        db.ApiKeys.Add(new ApiKey
        {
            Id = adminId,
            Name = "Bootstrap Admin",
            Role = "Admin",
            Scopes = "firmware.read_all,firmware.write",
            Hash = hash,
            Salt = salt,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = null
        });

        db.SaveChanges();
    }
}
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();