using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Tinkwell.Firmwareless;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAspireBlobContainerClientFactory(this IServiceCollection services)
        => services.AddSingleton<IBlobContainerClientFactory, BlobContainerClientFactory>();

    public static IServiceCollection AddAspireBlobContainerClient(this IServiceCollection services, string referenceName)
    {
        services.AddSingleton(serviceProvider =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString(referenceName)
                     ?? throw new InvalidOperationException($"Missing connection string '{referenceName}'.");
            return new BlobContainerClient(connectionString, referenceName);
        });
        
        return services;
    }

    public static IServiceCollection AddAspireHttpClient(this IServiceCollection services, string referenceName)
    {
        services.AddHttpClient(referenceName, (client) =>
        {
            client.BaseAddress = new Uri($"https://{referenceName}");
        }).AddServiceDiscovery();

        return services;
    }
}
