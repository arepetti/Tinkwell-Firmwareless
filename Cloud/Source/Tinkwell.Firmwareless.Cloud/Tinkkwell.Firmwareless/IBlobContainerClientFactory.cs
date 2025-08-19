using Azure.Storage.Blobs;

namespace Tinkkwell.Firmwareless;

public interface IBlobContainerClientFactory
{
    BlobContainerClient GetBlobContainerClient(string referenceName);
}
