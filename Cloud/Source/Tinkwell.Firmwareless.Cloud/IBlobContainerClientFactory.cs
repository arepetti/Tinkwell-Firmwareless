using Azure.Storage.Blobs;

namespace Tinkwell.Firmwareless;

public interface IBlobContainerClientFactory
{
    BlobContainerClient GetBlobContainerClient(string referenceName);
}
