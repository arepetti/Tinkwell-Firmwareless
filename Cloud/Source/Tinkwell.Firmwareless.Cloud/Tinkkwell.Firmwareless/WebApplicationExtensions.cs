using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Tinkkwell.Firmwareless;

public static class WebApplicationExtensions
{
    public static WebApplication AddExceptionLogging(this WebApplication app)
    {
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
                if (feature?.Error is Exception ex)
                    logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);

                await Results.Problem().ExecuteAsync(context);
            });
        });

        return app;
    }

    public static WebApplication AddFailedResponseLogging(this WebApplication app)
    {
        app.Use(async (ctx, next) =>
        {
            var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await next();
            }
            finally
            {
                sw.Stop();
                var status = ctx.Response.StatusCode;
                if (status >= 400)
                {
                    logger.LogWarning("Failed response {Status} for {Method} {Path} in {Elapsed} ms",
                        status, ctx.Request.Method, ctx.Request.Path, sw.ElapsedMilliseconds);
                }
            }
        });

        return app;
    }
}