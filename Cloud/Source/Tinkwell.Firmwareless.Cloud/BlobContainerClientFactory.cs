using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;

namespace Tinkwell.Firmwareless;

sealed class BlobContainerClientFactory : IBlobContainerClientFactory
{
    public BlobContainerClientFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    public BlobContainerClient GetBlobContainerClient(string referenceName)
    {
        var connectionString = _configuration.GetConnectionString(referenceName)
                 ?? throw new InvalidOperationException($"Missing connection string '{referenceName}'.");
        return new BlobContainerClient(connectionString, referenceName);
    }

    private readonly IConfiguration _configuration;
}
