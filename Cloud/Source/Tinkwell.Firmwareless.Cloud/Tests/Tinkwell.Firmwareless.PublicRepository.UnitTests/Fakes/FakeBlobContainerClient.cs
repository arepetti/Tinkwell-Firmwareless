using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Moq;

namespace Tinkwell.Firmwareless.PublicRepository.UnitTests.Fakes;

// A fake client to control the behavior of blob uploads for tests.
public class FakeBlobContainerClient : BlobContainerClient
{
    public bool ShouldThrowOnUpload { get; set; }
    public bool UploadCalled { get; private set; }

    // Provide a base constructor call. The URI can be a dummy one.
    public FakeBlobContainerClient() : base(new Uri("https://fake.blob.core.windows.net/fake-container"), null as BlobClientOptions) { }

    public override BlobClient GetBlobClient(string blobName)
    {
        var mockBlobClient = new Mock<BlobClient>();

        // Set up the Uri property to return a fake URI
        mockBlobClient.SetupGet(c => c.Uri)
                      .Returns(new Uri("https://fakeaccount.blob.core.windows.net/fakecontainer/fakeblob"));

        // When UploadAsync is called on the blob client, we intercept it.
        mockBlobClient.Setup(c => c.UploadAsync(
            It.IsAny<Stream>(),
            true, // Overwrite is true in FirmwaresService
            It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                UploadCalled = true;
                if (ShouldThrowOnUpload)
                {
                    throw new RequestFailedException("Simulated upload failure.");
                }
            })
            .ReturnsAsync(() =>
            {
                // Return a faked successful response.
                var mockBlobContentInfo = new Mock<BlobContentInfo>();
                var mockResponse = new Mock<Response>();
                return Response.FromValue(mockBlobContentInfo.Object, mockResponse.Object);
            });

        return mockBlobClient.Object;
    }

    public override Response<BlobContainerInfo> CreateIfNotExists(PublicAccessType publicAccessType = PublicAccessType.None, IDictionary<string, string> metadata = null!, BlobContainerEncryptionScopeOptions encryptionScopeOptions = null!, CancellationToken cancellationToken = default)
        => null!;

    public override Response<BlobContainerInfo> CreateIfNotExists(PublicAccessType publicAccessType, IDictionary<string, string> metadata, CancellationToken cancellationToken)
        => null!;

    public override Task<Response<BlobContainerInfo>> CreateIfNotExistsAsync(PublicAccessType publicAccessType = PublicAccessType.None, IDictionary<string, string> metadata = null!, BlobContainerEncryptionScopeOptions encryptionScopeOptions = null!, CancellationToken cancellationToken = default)
        => Task.FromResult<Response<BlobContainerInfo>>(null!);

    public override Task<Response<BlobContainerInfo>> CreateIfNotExistsAsync(PublicAccessType publicAccessType, IDictionary<string, string> metadata, CancellationToken cancellationToken)
        => Task.FromResult<Response<BlobContainerInfo>>(null!);
}