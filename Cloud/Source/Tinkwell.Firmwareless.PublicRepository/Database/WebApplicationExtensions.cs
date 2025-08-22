using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tinkwell.Firmwareless.PublicRepository.Configuration;
using Tinkwell.Firmwareless.PublicRepository.Services;

namespace Tinkwell.Firmwareless.PublicRepository.Database;

static class WebApplicationExtensions
{
    private const int MaxRetries = 10;
    private const int DelayBetweenRetries = 1000;

    public static async Task ApplyMigrationsAsync(this WebApplication app, IServiceScope scope)
    {
        // Do not run migrations in test environment
        if (app.Environment.IsEnvironment("Testing"))
            return; 

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var apiOpts = scope.ServiceProvider.GetRequiredService<IOptions<ApiKeyOptions>>().Value;

        for (int attempt = 1; attempt <= MaxRetries; ++attempt)
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

                if (attempt == MaxRetries)
                {
                    app.Logger.LogInformation("Max retries reached. Giving up.");
                    throw;
                }

                await Task.Delay(DelayBetweenRetries * attempt);
            }
        }
    }
    
    public static async Task AddProvisioningKeys(this WebApplication app, IServiceScope scope)
    {
        // Do not add bootstrapping keys in the test environment
        if (app.Environment.IsEnvironment("Testing"))
            return; 
        
        // Bootstrap Admin key, it's strictly temporary! It should be replaced with a proper key
        // as soon as possible and then revoked. To use only for bootstrapping and for local testing.
        var keyService = scope.ServiceProvider.GetRequiredService<KeysService>();
        if (await keyService.HasAdminKeyAsync() == false)
        {
            var adminKey = await keyService.UnsafeCreateForAdminAsync();
            Console.WriteLine($"[BOOTSTRAP] Admin API key (store securely): {adminKey}");

            var hubKey = await keyService.UnsafeCreateForHubAsync();
            Console.WriteLine($"[BOOTSTRAP] Hub API key (store securely): {hubKey}");
        }
    }
}