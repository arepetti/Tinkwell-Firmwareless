using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Tinkkwell.Firmwareless;

public static class WebApplicationBuilderExtensions
{
    public static WebApplicationBuilder AddDefaultLogging(this WebApplicationBuilder builder)
    {
        builder.Configuration
    .       AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        return builder;
    }

    public static WebApplicationBuilder AddInvalidModelStateLogging(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

                foreach (var entry in context.ModelState.Where(kv => kv.Value?.Errors.Count > 0))
                {
                    logger.LogWarning("Model binding/validation failed for {Method} {Path}. Field: {Field}. Errors: {Errors}",
                        context.HttpContext.Request.Method,
                        context.HttpContext.Request.Path,
                        entry.Key,
                        string.Join("\n", entry.Value!.Errors.Select(e => e.ErrorMessage)));
                }

                var problem = new Microsoft.AspNetCore.Mvc.ValidationProblemDetails(context.ModelState)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Request validation failed"
                };
                return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(problem);
            };
        });

        return builder;
    }
}
