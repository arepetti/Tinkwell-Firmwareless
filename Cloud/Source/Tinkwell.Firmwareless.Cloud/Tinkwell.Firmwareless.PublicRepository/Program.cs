using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
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

builder.Services.AddServiceDiscovery();

builder.Services.AddHttpClient("tinkwell-compilation-server", (client) =>
{
    client.BaseAddress = new Uri("https://tinkwell-compilation-server");
});

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

var app = builder.Build();

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