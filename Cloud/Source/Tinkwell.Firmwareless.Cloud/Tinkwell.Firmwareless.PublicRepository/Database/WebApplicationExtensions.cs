using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tinkwell.Firmwareless.PublicRepository.Authentication;

namespace Tinkwell.Firmwareless.PublicRepository.Database;

static class WebApplicationExtensions
{
    public static async Task ApplyMigrationsAsync(this WebApplication app, IServiceScope scope)
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
    }
}