using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Linq;
using System.Text.Json.Serialization;
using Tinkwell.Firmwareless.PublicRepository.Database;
using Tinkwell.Firmwareless.PublicRepository.IntegrationTests.Fakes;
using Tinkwell.Firmwareless.PublicRepository.Services;
using Tinkwell.Firmwareless.PublicRepository.UnitTests.Fakes;

namespace Tinkwell.Firmwareless.PublicRepository.IntegrationTests;

public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove original services
            var dbContextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbContextDescriptor != null) services.Remove(dbContextDescriptor);

            var proxyDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(CompilationProxyService));
            if (proxyDescriptor != null) services.Remove(proxyDescriptor);

            var blobClientDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(Azure.Storage.Blobs.BlobContainerClient));
            if (blobClientDescriptor != null) services.Remove(blobClientDescriptor);

            // Add test services
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase("InMemoryDbForIntegrationTesting");
            });

            services.AddSingleton<CompilationProxyService, FakeCompilationProxyService>();
            services.AddSingleton<Azure.Storage.Blobs.BlobContainerClient, FakeBlobContainerClient>();

            services.PostConfigure<Microsoft.AspNetCore.Http.Json.JsonOptions>(o =>
            {
                o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

            // Ensure the database is created.
            var sp = services.BuildServiceProvider();
            using (var scope = sp.CreateScope())
            {
                var scopedServices = scope.ServiceProvider;
                var db = scopedServices.GetRequiredService<AppDbContext>();
                db.Database.EnsureCreated();
            }
        });
    }
}
